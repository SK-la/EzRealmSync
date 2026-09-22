using System.Text.Json;
using System.Text.Json.Serialization;
using Realms;
using Realms.Schema;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// schema 快照的本地仓库：访问任何 Realm 时顺手落盘，于是「碰过哪个版本就有哪个版本的快照」。
    ///
    /// 快照只服务三件事——识别库类型、判定写集、证明写入前后 schema 未变。它不参与版本拒绝，
    /// 也不作为"能不能打开"的依据；采集失败只记日志，绝不影响调用方正在做的读写。
    /// </summary>
    public sealed class RealmSchemaSnapshotStore
    {
        public const string FileSuffix = ".schema.json";

        /// <summary>产品路径（工具目录下 snapshots/）。测试传临时目录，避免写进真实工具目录。</summary>
        public static RealmSchemaSnapshotStore Default { get; } = new RealmSchemaSnapshotStore();

        public RealmSchemaSnapshotStore(string? directory = null) =>
            DirectoryPath = directory ?? EzRealmSyncDataPaths.SnapshotsDirectory;

        public string DirectoryPath { get; }

        public string FilePathFor(int diskSchemaVersion) =>
            Path.Combine(DirectoryPath, $"{diskSchemaVersion}{FileSuffix}");

        public bool Exists(int diskSchemaVersion) => File.Exists(FilePathFor(diskSchemaVersion));

        /// <summary>
        /// 显式采集（用户指定某个库生成快照）。读不出内容或版本时抛异常，
        /// 让调用方能如实告诉用户失败原因——这条路径不是"顺手采集"，不允许静默失败。
        /// </summary>
        public RealmSchemaSnapshotFile Capture(string realmFilePath)
        {
            using var session = DynamicRealmSession.OpenDynamic(realmFilePath, readOnly: true);
            return capture(session);
        }

        /// <summary>
        /// 库里已有该版本的快照时默认不动它（同一版本只留第一份）；失败只记日志。
        /// 传 <paramref name="overwrite"/> 用于"我刚写过的库，刷新一下"。
        /// </summary>
        public RealmSchemaSnapshotFile? TryCapture(DynamicRealmSession session, bool overwrite = false)
        {
            try
            {
                if (session.DiskSchemaVersion <= 0)
                    return null;

                if (!overwrite && Exists(session.DiskSchemaVersion))
                    return null;

                return capture(session);
            }
            catch (Exception ex)
            {
                EzRealmSyncLog.Warn($"schema 快照采集失败（不影响当前操作）：{session.FilePath} — {ex.Message}");
                return null;
            }
        }

        /// <summary>按路径采集（自己开一次只读动态会话）。用于浏览这类本来要另开一次的调用点。</summary>
        public RealmSchemaSnapshotFile? TryCapture(string realmFilePath, bool overwrite = false)
        {
            try
            {
                using var session = DynamicRealmSession.OpenDynamic(realmFilePath, readOnly: true);
                return TryCapture(session, overwrite);
            }
            catch (Exception ex)
            {
                EzRealmSyncLog.Warn($"schema 快照采集失败（不影响当前操作）：{realmFilePath} — {ex.Message}");
                return null;
            }
        }

        public RealmSchemaSnapshotFile? Load(int diskSchemaVersion)
        {
            string path = FilePathFor(diskSchemaVersion);
            if (!File.Exists(path))
                return null;

            try
            {
                var dto = JsonSerializer.Deserialize<SnapshotDto>(File.ReadAllText(path), json_options);
                return dto == null ? null : fromDto(dto);
            }
            catch (Exception ex)
            {
                // 半截/手工改坏的 JSON 当成"没有这份快照"：调用方要么重新采集，要么走保守回退。
                EzRealmSyncLog.Warn($"schema 快照读取失败，按不存在处理：{path} — {ex.Message}");
                return null;
            }
        }

        public IReadOnlyList<RealmSchemaSnapshotFile> LoadAll()
        {
            if (!Directory.Exists(DirectoryPath))
                return Array.Empty<RealmSchemaSnapshotFile>();

            var loaded = new List<RealmSchemaSnapshotFile>();

            foreach (string path in Directory.EnumerateFiles(DirectoryPath, "*" + FileSuffix))
            {
                string stem = Path.GetFileName(path);
                if (!int.TryParse(stem[..^FileSuffix.Length], out int version))
                    continue;

                if (Load(version) is { } file)
                    loaded.Add(file);
            }

            return loaded.OrderBy(f => f.DiskSchemaVersion).ToArray();
        }

        /// <summary>
        /// 该版本能否当「官方 N 的事实来源」（判据见 <see cref="RealmSchemaSnapshotClassifier"/>）。
        /// 同步写集与转官方据此决定"官方基线有哪些格子"，没有快照时由调用方走保守回退。
        /// </summary>
        public bool TryLoadOfficialBaseline(int diskSchemaVersion, out RealmSchemaSnapshot? officialSnapshot, out int officialUpstream)
        {
            officialSnapshot = null;
            officialUpstream = 0;

            if (Load(diskSchemaVersion) is not { } file)
                return false;

            if (!RealmSchemaSnapshotClassifier.TryGetOfficialUpstream(file.DiskSchemaVersion, file.Snapshot, out int upstream))
                return false;

            officialSnapshot = file.Snapshot;
            officialUpstream = upstream;
            return true;
        }

        public void Save(RealmSchemaSnapshotFile snapshot)
        {
            Directory.CreateDirectory(DirectoryPath);

            string path = FilePathFor(snapshot.DiskSchemaVersion);
            string temp = path + ".tmp";

            // 先写临时文件再替换：中途失败不会留下"看起来像快照"的半截 JSON。
            File.WriteAllText(temp, JsonSerializer.Serialize(toDto(snapshot), json_options));
            File.Move(temp, path, overwrite: true);
        }

        private RealmSchemaSnapshotFile capture(DynamicRealmSession session)
        {
            if (session.DiskSchemaVersion <= 0)
                throw new InvalidOperationException($"无法从文件头读出 Realm schema 版本：{session.FilePath}");

            var file = new RealmSchemaSnapshotFile(
                session.DiskSchemaVersion,
                DynamicSchemaReader.Read(session),
                session.FilePath,
                DateTimeOffset.UtcNow);

            Save(file);
            EzRealmSyncLog.Info($"schema 快照已写入 {FilePathFor(file.DiskSchemaVersion)}（类 {file.Snapshot.ClassCount} 个，来源 {file.SourcePath}）");

            return file;
        }

        private static SnapshotDto toDto(RealmSchemaSnapshotFile file) => new()
        {
            DiskSchemaVersion = file.DiskSchemaVersion,
            SourcePath = file.SourcePath,
            CollectedAtUtc = file.CollectedAtUtc,
            Classes = file.Snapshot.Classes.Select(c => new ClassDto
            {
                Name = c.Name,
                Embedded = c.IsEmbedded,
                PrimaryKey = c.PrimaryKeyProperty,
                Properties = c.Properties.Select(p => new PropertyDto
                {
                    Name = p.Name,
                    Type = (int)p.Type,
                    TypeText = p.DescribeType(),
                    ObjectType = p.ObjectType,
                    LinkOrigin = p.LinkOriginPropertyName,
                    PrimaryKey = p.IsPrimaryKey,
                    Index = (int)p.Index,
                }).ToList(),
            }).ToList(),
        };

        private static RealmSchemaSnapshotFile fromDto(SnapshotDto dto) => new(
            dto.DiskSchemaVersion,
            new RealmSchemaSnapshot(dto.Classes.Select(c => new RealmClassSchema(
                c.Name,
                c.Embedded,
                c.Properties.Select(p => new RealmPropertySchema(
                    p.Name,
                    (PropertyType)p.Type,
                    p.ObjectType ?? string.Empty,
                    p.LinkOrigin,
                    p.PrimaryKey,
                    (IndexType)p.Index))))),
            dto.SourcePath,
            dto.CollectedAtUtc);

        private static readonly JsonSerializerOptions json_options = new()
        {
            WriteIndented = true,
        };

        private sealed class SnapshotDto
        {
            [JsonPropertyName("diskSchemaVersion")]
            public int DiskSchemaVersion { get; set; }

            [JsonPropertyName("sourcePath")]
            public string? SourcePath { get; set; }

            [JsonPropertyName("collectedAtUtc")]
            public DateTimeOffset CollectedAtUtc { get; set; }

            [JsonPropertyName("classes")]
            public List<ClassDto> Classes { get; set; } = new List<ClassDto>();
        }

        private sealed class ClassDto
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("embedded")]
            public bool Embedded { get; set; }

            [JsonPropertyName("primaryKey")]
            public string? PrimaryKey { get; set; }

            [JsonPropertyName("properties")]
            public List<PropertyDto> Properties { get; set; } = new List<PropertyDto>();
        }

        private sealed class PropertyDto
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            /// <summary>PropertyType 的原始 flags 值——权威字段。枚举名会因位重合产生歧义（见 DescribeType）。</summary>
            [JsonPropertyName("type")]
            public int Type { get; set; }

            /// <summary>人读形式（<c>Array&lt;String?&gt;</c> 之类），加载时忽略。</summary>
            [JsonPropertyName("typeText")]
            public string? TypeText { get; set; }

            [JsonPropertyName("objectType")]
            public string? ObjectType { get; set; }

            [JsonPropertyName("linkOrigin")]
            public string? LinkOrigin { get; set; }

            [JsonPropertyName("primaryKey")]
            public bool PrimaryKey { get; set; }

            [JsonPropertyName("index")]
            public int Index { get; set; }
        }
    }
}
