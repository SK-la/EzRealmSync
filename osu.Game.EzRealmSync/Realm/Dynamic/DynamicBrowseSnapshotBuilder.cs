using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using osu.Game.EzRealmSync.Models;
using Realms;

namespace osu.Game.EzRealmSync.Realm.Dynamic
{
    /// <summary>
    /// 数据页浏览快照的**动态**构建：不加载 osu.Game 模型，按磁盘 schema 逐列取名读值。
    ///
    /// 列清单仍是策展的（数据页是浏览视图，官方 schema 下 Beatmap 有近 30 列、File 表可到几十万行，
    /// 全列铺进表格既读不动也没人看），但每列都写成**列路径**并在运行时对着 schema 解析：
    /// 路径取不到就留空、类型不符就走归一，不会因为库的版本变化而崩，也不再需要 typed 模型。
    /// 「任意列可读」由 <see cref="DynamicValueCodec"/> 保证，这里只决定默认显示哪几列。
    ///
    /// 显示值与旧 typed 版本逐列对齐（日期、星数两位小数、成绩千分位/百分比、链接列显示对方某一列）。
    /// 唯一有意的差别是枚举列直接显示**库里存的数值**——把枚举名搬进工具就等于手抄一份会漂移的官方表，
    /// 而这几个列在同步侧本来也是按 int 处理的（官方库的浏览一直就是这么显示的）。
    /// </summary>
    public static class DynamicBrowseSnapshotBuilder
    {
        public static RealmSnapshot Build(
            RealmFileEntry file,
            DynamicRealmSession session,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(file);
            ArgumentNullException.ThrowIfNull(session);

            progress?.Report(new ScanProgress { Progress = 0, Message = "正在读取类型…" });
            cancellationToken.ThrowIfCancellationRequested();

            RealmSchemaSnapshot schema = DynamicSchemaReader.Read(session);

            var classes = new List<RealmClassGroup>
            {
                readKeyedClass(session, schema, class_specs[0]),
                readKeyedClass(session, schema, class_specs[1]),
                readMetadata(session, schema),
                readKeyedClass(session, schema, class_specs[2]),
                readKeyedClass(session, schema, class_specs[3]),
                readFiles(session, schema),
                readKeyedClass(session, schema, class_specs[4]),
                readKeyedClass(session, schema, class_specs[5]),
            };

            progress?.Report(new ScanProgress { Progress = 1, Message = "加载完成" });

            return new RealmSnapshot
            {
                RealmId = file.Id,
                DisplayName = file.DisplayName,
                Classes = classes,
                Groups = RealmSnapshotGrouper.DeriveGroups(classes),
            };
        }

        private static RealmClassGroup readKeyedClass(DynamicRealmSession session, RealmSchemaSnapshot schema, ClassSpec spec)
        {
            if (!schema.TryFindClass(spec.ClassName, out _))
                return emptyGroup(spec);

            var rows = new List<RealmBrowseRow>();

            foreach (IRealmObjectBase row in DynamicRowAccess.LiveRows(session, schema, spec.ClassName))
            {
                rows.Add(new RealmBrowseRow
                {
                    Id = readRowId(row, schema, spec),
                    Cells = readCells(row, schema, spec.Columns),
                });
            }

            return new RealmClassGroup
            {
                Class = spec.Class,
                Columns = toColumnDefinitions(spec.Columns),
                Rows = rows,
            };
        }

        /// <summary>
        /// 元数据在库里是嵌入类（没有独立表），旧 typed 版本按「标题 + 作者」去重后当成一类展示。保持同样做法。
        /// </summary>
        private static RealmClassGroup readMetadata(DynamicRealmSession session, RealmSchemaSnapshot schema)
        {
            var rows = new List<RealmBrowseRow>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            if (schema.HasClass(OfficialBaselineSchema.Beatmap) && schema.HasClass(metadata_class))
            {
                foreach (IRealmObjectBase beatmap in DynamicRowAccess.LiveRows(session, schema, OfficialBaselineSchema.Beatmap))
                {
                    // 缺失的标题 / 作者按空串参与去重：旧 typed 版本也把它们当空串拼 key，这里保持同一套行标识。
                    string title = DynamicRowAccess.ResolveString(beatmap, schema, metadata_title_path) ?? string.Empty;
                    string artist = DynamicRowAccess.ResolveString(beatmap, schema, metadata_artist_path) ?? string.Empty;

                    if (!seen.Add($"{title}\0{artist}"))
                        continue;

                    var cells = new Dictionary<string, string>(StringComparer.Ordinal);

                    foreach (ColumnSpec column in metadata_columns)
                        cells[column.Key] = format(DynamicRowAccess.Resolve(beatmap, schema, column.Paths[0]), column.Format);

                    rows.Add(new RealmBrowseRow { Id = toRowId($"{title}\0{artist}"), Cells = cells });
                }
            }

            return new RealmClassGroup
            {
                Class = RealmObjectClass.BeatmapMetadata,
                Columns = toColumnDefinitions(metadata_columns),
                Rows = rows,
            };
        }

        /// <summary>
        /// 文件分组要单独处理：盘上的 <c>File</c> 表只有 <c>Hash</c> 一列，没有「这个文件被谁用、叫什么名字」这种反向信息
        /// ——旧 typed 版本的 <c>Usages</c> 是模型里的 Backlink 现算的，动态打开看不到。
        /// 这里改为扫一遍三个持有者（谱面集 / 成绩 / 皮肤）的 <c>Files</c> 列表，得到同一份 hash → 文件名 映射。
        /// </summary>
        private static RealmClassGroup readFiles(DynamicRealmSession session, RealmSchemaSnapshot schema)
        {
            if (!schema.HasClass(OfficialBaselineSchema.File))
                return emptyGroup(file_spec);

            IReadOnlyDictionary<string, string> names = buildFileNames(session, schema);
            var rows = new List<RealmBrowseRow>();

            foreach (IRealmObjectBase row in DynamicRowAccess.AllRows(session, schema, OfficialBaselineSchema.File))
            {
                string hash = DynamicRowAccess.ResolveString(row, schema, "Hash") ?? string.Empty;

                rows.Add(new RealmBrowseRow
                {
                    Id = toRowId(hash),
                    Cells = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["Filename"] = names.TryGetValue(hash, out string? name) ? name : hash,
                        ["Hash"] = hash,
                        ["Present"] = names.ContainsKey(hash) ? "true" : "false",
                    },
                });
            }

            return new RealmClassGroup
            {
                Class = RealmObjectClass.File,
                Columns = toColumnDefinitions(file_spec.Columns),
                Rows = rows,
            };
        }

        private static IReadOnlyDictionary<string, string> buildFileNames(DynamicRealmSession session, RealmSchemaSnapshot schema)
        {
            var names = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (string holder in file_holders)
            {
                if (!schema.HasClass(holder))
                    continue;

                foreach (IRealmObjectBase row in DynamicRowAccess.AllRows(session, schema, holder))
                {
                    if (DynamicRowAccess.Resolve(row, schema, "Files") is not System.Collections.IEnumerable usages)
                        continue;

                    foreach (object? usage in usages)
                    {
                        if (usage is not IRealmObjectBase usageObject)
                            continue;

                        if (DynamicRowAccess.ResolveString(usageObject, schema, "File.Hash") is not string hash)
                            continue;

                        if (DynamicRowAccess.ResolveString(usageObject, schema, "Filename") is string name)
                            names.TryAdd(hash, name);
                    }
                }
            }

            return names;
        }

        private static RealmClassGroup emptyGroup(ClassSpec spec) => new RealmClassGroup
        {
            Class = spec.Class,
            Columns = toColumnDefinitions(spec.Columns),
            Rows = Array.Empty<RealmBrowseRow>(),
        };

        private static Dictionary<string, string> readCells(IRealmObjectBase row, RealmSchemaSnapshot schema, IReadOnlyList<ColumnSpec> columns)
        {
            var cells = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (ColumnSpec column in columns)
            {
                object? value = null;

                // 多路径即「取第一个有值的」：链接行可能缺失，退回到字符串列（旧 typed 版本的 ?? 写法）。
                foreach (string path in column.Paths)
                {
                    value = DynamicRowAccess.Resolve(row, schema, path);

                    if (value != null)
                        break;
                }

                cells[column.Key] = format(value, column.Format);
            }

            return cells;
        }

        /// <summary>行标识：主键是 Guid 就照用，否则（File.Hash、Ruleset.ShortName）沿用旧版的「哈希成 Guid」。</summary>
        private static Guid readRowId(IRealmObjectBase row, RealmSchemaSnapshot schema, ClassSpec spec)
        {
            object? key = DynamicRowAccess.Resolve(row, schema, spec.KeyProperty);

            return key switch
            {
                Guid guid => guid,
                null => GuidFromHash(string.Empty),
                _ => GuidFromHash(DynamicDumpValue.Describe(key)),
            };
        }

        private static string format(object? value, ValueFormat format)
        {
            // 浏览视图里「空」只有一种样子：旧 typed 版本读到的 null 字符串直接进表格显示为空，
            // 这里把 null 也落成空串，免得出现 <null> 这种只有 dump 才需要的记号。
            if (value == null && format == ValueFormat.Default)
                return string.Empty;

            switch (format)
            {
                case ValueFormat.Date:
                    return value switch
                    {
                        DateTimeOffset date => date.ToString("g", CultureInfo.CurrentCulture),
                        _ => DynamicDumpValue.Describe(value),
                    };

                case ValueFormat.Fixed2:
                    return value == null ? string.Empty : Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("F2", CultureInfo.InvariantCulture);

                case ValueFormat.Thousands:
                    return value == null ? string.Empty : Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString("N0", CultureInfo.CurrentCulture);

                case ValueFormat.Percent2:
                    return value == null ? string.Empty : Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("P2", CultureInfo.CurrentCulture);

                case ValueFormat.ListCount:
                    return countOf(value).ToString(CultureInfo.InvariantCulture);

                case ValueFormat.NotBlank:
                    return string.IsNullOrEmpty(value as string) ? "false" : "true";

                default:
                    return DynamicDumpValue.Describe(value);
            }
        }

        private static int countOf(object? value)
        {
            if (value is string || value is not System.Collections.IEnumerable enumerable)
                return 0;

            int count = 0;

            foreach (object? _ in enumerable)
                count++;

            return count;
        }

        private static IReadOnlyList<RealmColumnDefinition> toColumnDefinitions(IReadOnlyList<ColumnSpec> columns) =>
            columns.Select(c => new RealmColumnDefinition
            {
                Header = c.Header,
                PropertyKey = c.Key,
                TypeHint = c.TypeHint,
            }).ToArray();

        private static Guid toRowId(string key) => GuidFromHash(key);

        /// <summary>非 Guid 主键（File.Hash、Ruleset.ShortName、去重后的元数据）沿用旧版的「哈希成 Guid」做法，只为行标识稳定。</summary>
        private static Guid GuidFromHash(string key)
        {
            byte[] bytes = MD5.HashData(Encoding.UTF8.GetBytes(key));
            return new Guid(bytes);
        }

        private const string metadata_class = "BeatmapMetadata";
        private const string metadata_title_path = "Metadata.Title";
        private const string metadata_artist_path = "Metadata.Artist";

        /// <summary>持有 <c>Files</c> 列表的类（osu 里只有这三个），用来反推文件名。</summary>
        private static readonly string[] file_holders =
        [
            OfficialBaselineSchema.BeatmapSet,
            OfficialBaselineSchema.Score,
            OfficialBaselineSchema.Skin,
        ];

        private static readonly ClassSpec file_spec = new ClassSpec(
            RealmObjectClass.File,
            OfficialBaselineSchema.File,
            "Hash",
            [
                // 只声明列（键 / 标题 / 类型提示）；这些格子的值由 readFiles 按 hash → 文件名 映射填。
                new ColumnSpec("Filename", "Filename", "string", ValueFormat.Default, "Hash"),
                new ColumnSpec("Hash", "Hash", "string", ValueFormat.Default, "Hash"),
                new ColumnSpec("Present", "Present", "bool", ValueFormat.Default, "Hash"),
            ]);

        private enum ValueFormat
        {
            Default,
            Date,
            Fixed2,
            Thousands,
            Percent2,
            ListCount,
            NotBlank,
        }

        private sealed record ColumnSpec(string Key, string Header, string TypeHint, ValueFormat Format, params string[] Paths);

        private sealed record ClassSpec(
            RealmObjectClass Class,
            string ClassName,
            string KeyProperty,
            IReadOnlyList<ColumnSpec> Columns);

        private static readonly IReadOnlyList<ColumnSpec> metadata_columns =
        [
            new ColumnSpec("Title", "Title", "string", ValueFormat.Default, metadata_title_path),
            new ColumnSpec("Artist", "Artist", "string", ValueFormat.Default, metadata_artist_path),
            new ColumnSpec("Source", "Source", "string", ValueFormat.Default, "Metadata.Source"),
            new ColumnSpec("Tags", "Tags", "string", ValueFormat.Default, "Metadata.Tags"),
        ];

        // 顺序即数据页各类型 Tab 的顺序：谱面集、难度、元数据、成绩、收藏夹、文件、规则集、皮肤。
        // 文件不在这里：盘上 File 表只有 Hash，文件名要另算（见 readFiles）。
        private static readonly IReadOnlyList<ClassSpec> class_specs =
        [
            new ClassSpec(RealmObjectClass.BeatmapSet, OfficialBaselineSchema.BeatmapSet, "ID",
            [
                new ColumnSpec("Hash", "Hash", "string", ValueFormat.Default, "Hash"),
                new ColumnSpec("OnlineID", "Online ID", "int", ValueFormat.Default, "OnlineID"),
                new ColumnSpec("DateAdded", "Date added", "date", ValueFormat.Date, "DateAdded"),
                new ColumnSpec("Status", "Status", "enum", ValueFormat.Default, "Status"),
            ]),
            new ClassSpec(RealmObjectClass.Beatmap, OfficialBaselineSchema.Beatmap, "ID",
            [
                new ColumnSpec("Hash", "Hash", "string", ValueFormat.Default, "Hash"),
                new ColumnSpec("StarRating", "Stars", "double", ValueFormat.Fixed2, "StarRating"),
                new ColumnSpec("Ruleset", "Ruleset", "object", ValueFormat.Default, "Ruleset.ShortName"),
                new ColumnSpec("BeatmapSet", "BeatmapSet", "object", ValueFormat.Default, "BeatmapSet.Hash"),
            ]),
            new ClassSpec(RealmObjectClass.Score, OfficialBaselineSchema.Score, "ID",
            [
                new ColumnSpec("Date", "Date", "date", ValueFormat.Date, "Date"),
                new ColumnSpec("TotalScore", "Total score", "int", ValueFormat.Thousands, "TotalScore"),
                new ColumnSpec("Accuracy", "Accuracy", "double", ValueFormat.Percent2, "Accuracy"),
                new ColumnSpec("Ruleset", "Ruleset", "object", ValueFormat.Default, "Ruleset.ShortName"),
                new ColumnSpec("Beatmap", "Beatmap", "object", ValueFormat.Default, "BeatmapInfo.Hash", "BeatmapHash"),
            ]),
            new ClassSpec(RealmObjectClass.BeatmapCollection, OfficialBaselineSchema.BeatmapCollection, "ID",
            [
                new ColumnSpec("Name", "Name", "string", ValueFormat.Default, "Name"),
                new ColumnSpec("BeatmapHashes", "Beatmaps", "list", ValueFormat.ListCount, "BeatmapMD5Hashes"),
            ]),
            new ClassSpec(RealmObjectClass.Ruleset, OfficialBaselineSchema.Ruleset, "ShortName",
            [
                new ColumnSpec("ShortName", "Short name", "string", ValueFormat.Default, "ShortName"),
                new ColumnSpec("Name", "Name", "string", ValueFormat.Default, "Name"),
                new ColumnSpec("Available", "Available", "bool", ValueFormat.NotBlank, "InstantiationInfo"),
            ]),
            new ClassSpec(RealmObjectClass.Skin, OfficialBaselineSchema.Skin, "ID",
            [
                new ColumnSpec("Name", "Name", "string", ValueFormat.Default, "Name"),
                new ColumnSpec("Creator", "Creator", "string", ValueFormat.Default, "Creator"),
            ]),
        ];
    }
}
