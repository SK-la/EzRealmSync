using osu.Game.EzRealmSync.Models;
using Realms;

namespace osu.Game.EzRealmSync.Realm.Dynamic
{
    /// <summary>
    /// 导出目录（列表 + 目标相对路径）的**动态**构建：不加载 osu.Game 模型。
    ///
    /// 这里出现的是"产品文案"（标题格式、导出文件名），必须与官方展示规则逐字一致，
    /// 统一走 <see cref="DynamicDisplayText"/>，不要在别处再拼一份。
    /// </summary>
    internal static class DynamicExportCatalogBuilder
    {
        public static RealmExportCatalog Build(
            DynamicRealmSession session,
            RealmSchemaSnapshot schema,
            ExportDataKind kind,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var items = new List<RealmExportItem>();

            switch (kind)
            {
                case ExportDataKind.BeatmapSet:
                    addBeatmapSets(session, schema, items, progress, cancellationToken);
                    break;

                case ExportDataKind.Beatmap:
                    addBeatmaps(session, schema, items, progress, cancellationToken);
                    break;

                case ExportDataKind.Collection:
                case ExportDataKind.CollectionDb:
                    addCollections(session, schema, items, progress, cancellationToken);
                    break;

                case ExportDataKind.Score:
                    addScores(session, schema, items, progress, cancellationToken, requireReplayFile: true);
                    break;

                case ExportDataKind.ScoreDb:
                    addScores(session, schema, items, progress, cancellationToken, requireReplayFile: false);
                    break;
            }

            progress?.Report(new ScanProgress { Progress = 1, Message = "列表加载完成" });
            return new RealmExportCatalog { Kind = kind, Items = items };
        }

        private static void addBeatmapSets(
            DynamicRealmSession session,
            RealmSchemaSnapshot schema,
            List<RealmExportItem> items,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            var sets = DynamicRowAccess.LiveRows(session, schema, OfficialBaselineSchema.BeatmapSet).ToList();

            int index = 0;

            foreach (IRealmObjectBase set in sets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 旧 typed 版本取 set.Beatmaps.FirstOrDefault()：没有难度就没有可导出的路径，跳过。
                if (firstListElement(set, schema, "Beatmaps") is not IRealmObjectBase beatmap)
                    continue;

                string? hash = DynamicRowAccess.ResolveString(beatmap, schema, "Hash");
                if (string.IsNullOrEmpty(hash))
                    continue;

                // 谱面集自己没有持久化的元数据列：官方 <c>BeatmapSetInfo.Metadata</c> 就是
                // <c>Beatmaps.FirstOrDefault()?.Metadata ?? new BeatmapMetadata()</c>，这里按同一规则取，
                // 不能用 "Metadata.Title" 去解（那条路径在库的 schema 里不存在，只会得到空标题）。
                string title = metadataField(beatmap, schema, "Title") ?? string.Empty;
                string artist = metadataField(beatmap, schema, "Artist") ?? string.Empty;
                string author = metadataAuthor(beatmap, schema) ?? string.Empty;

                report(progress, ++index, sets.Count, DynamicDisplayText.MetadataTitle(artist, title, author));

                items.Add(new RealmExportItem
                {
                    Id = readId(set, schema),
                    Title = title,
                    Artist = artist,
                    RelativePath = RealmFilePathHelper.GetStoragePath(hash),
                });
            }
        }

        private static void addBeatmaps(
            DynamicRealmSession session,
            RealmSchemaSnapshot schema,
            List<RealmExportItem> items,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            var beatmaps = DynamicRowAccess.LiveRows(session, schema, OfficialBaselineSchema.Beatmap).ToList();

            int index = 0;

            foreach (IRealmObjectBase beatmap in beatmaps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string? hash = DynamicRowAccess.ResolveString(beatmap, schema, "Hash");
                if (string.IsNullOrEmpty(hash))
                    continue;

                string title = metadataField(beatmap, schema, "Title") ?? string.Empty;
                string artist = metadataField(beatmap, schema, "Artist") ?? string.Empty;
                string author = metadataAuthor(beatmap, schema) ?? string.Empty;

                report(progress, ++index, beatmaps.Count,
                    DynamicDisplayText.BeatmapTitle(artist, title, author, DynamicRowAccess.ResolveString(beatmap, schema, "DifficultyName")));

                items.Add(new RealmExportItem
                {
                    Id = readId(beatmap, schema),
                    Title = title,
                    Artist = artist,
                    RelativePath = RealmFilePathHelper.GetStoragePath(hash),
                });
            }
        }

        private static void addCollections(
            DynamicRealmSession session,
            RealmSchemaSnapshot schema,
            List<RealmExportItem> items,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            var collections = DynamicRowAccess.AllRows(session, schema, OfficialBaselineSchema.BeatmapCollection).ToList();

            int index = 0;

            foreach (IRealmObjectBase collection in collections)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string name = DynamicRowAccess.ResolveString(collection, schema, "Name") ?? string.Empty;
                int count = listCount(collection, schema, "BeatmapMD5Hashes");

                report(progress, ++index, collections.Count, name);

                items.Add(new RealmExportItem
                {
                    Id = readId(collection, schema),
                    Title = name,
                    Artist = string.Empty,
                    CollectionName = name,
                    BeatmapCount = count,
                });
            }
        }

        private static void addScores(
            DynamicRealmSession session,
            RealmSchemaSnapshot schema,
            List<RealmExportItem> items,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken,
            bool requireReplayFile)
        {
            var scores = DynamicRowAccess.LiveRows(session, schema, OfficialBaselineSchema.Score).ToList();

            int index = 0;

            foreach (IRealmObjectBase score in scores)
            {
                cancellationToken.ThrowIfCancellationRequested();

                RealmExportFileEntry? entry = requireReplayFile ? TryCreateScoreEntry(score, schema, groupScoresByPlayer: true) : null;

                // 只要 .osr 的导出（replays）里，没有回放文件的成绩不进列表；scores.db 列表则全展示。
                if (requireReplayFile && entry == null)
                    continue;

                string player = DynamicRowAccess.ResolveString(score, schema, "User.Username") ?? string.Empty;
                string title = ScoreDisplayTitle(score, schema);
                string? beatmapArtist = DynamicRowAccess.ResolveString(score, schema, "BeatmapInfo.Metadata.Artist");
                string beatmapHash = DynamicRowAccess.ResolveString(score, schema, "BeatmapHash") ?? string.Empty;

                report(progress, ++index, scores.Count, title);

                items.Add(new RealmExportItem
                {
                    Id = readId(score, schema),
                    Title = title,
                    Artist = beatmapArtist ?? beatmapHash,
                    PlayerName = player,
                    RelativePath = entry?.SourceRelative ?? string.Empty,
                    DestinationRelativePath = entry?.DestinationRelative,
                });
            }
        }

        /// <summary>
        /// 成绩导出条目：目标名 <c>{标题} ({本地时间}).osr</c>，可按玩家分目录。
        /// 返回 null 表示该成绩没有 .osr 引用（调用方据此跳过或留空）。
        /// </summary>
        public static RealmExportFileEntry? TryCreateScoreEntry(IRealmObjectBase score, RealmSchemaSnapshot schema, bool groupScoresByPlayer)
        {
            if (findReplayHash(score, schema) is not string replayHash)
                return null;

            string source = RealmFilePathHelper.GetStoragePath(replayHash);

            string displayTitle = ScoreDisplayTitle(score, schema);

            // 文件名里的日期用本地时间：官方导出同样用 LocalDateTime，改成 UTC 会让用户看到的文件名偏移。
            DateTimeOffset date = DynamicRowAccess.ResolveDate(score, schema, "Date") ?? default;
            string stamp = date.LocalDateTime.ToString("yyyy-MM-dd_HH-mm");
            string fileName = $"{DynamicDisplayText.ValidFilename(displayTitle)} ({stamp}).osr";

            string playerFolder = DynamicDisplayText.SanitizePathSegment(DynamicRowAccess.ResolveString(score, schema, "User.Username") ?? string.Empty);

            string destination = groupScoresByPlayer && !string.IsNullOrWhiteSpace(playerFolder)
                ? Path.Combine("replays", playerFolder, fileName)
                : Path.Combine("replays", fileName);

            return new RealmExportFileEntry
            {
                SourceRelative = source,
                DestinationRelative = destination,
            };
        }

        /// <summary>成绩标题：<c>玩家 playing 难度标题</c>；难度缺失按官方兜底 unknown。</summary>
        public static string ScoreDisplayTitle(IRealmObjectBase score, RealmSchemaSnapshot schema)
        {
            string? username = DynamicRowAccess.ResolveString(score, schema, "User.Username");
            string? beatmapTitle = beatmapDisplayTitle(score, schema);

            return DynamicDisplayText.ScoreTitle(username, beatmapTitle);
        }

        private static string? beatmapDisplayTitle(IRealmObjectBase score, RealmSchemaSnapshot schema)
        {
            // 优先走链接到的难度（列全时才有）；没有链接就只剩 BeatmapHash，无法拼标题。
            if (DynamicRowAccess.Resolve(score, schema, "BeatmapInfo") is not IRealmObjectBase beatmap)
                return null;

            return DynamicDisplayText.BeatmapTitle(
                metadataField(beatmap, schema, "Artist"),
                metadataField(beatmap, schema, "Title"),
                metadataAuthor(beatmap, schema),
                DynamicRowAccess.ResolveString(beatmap, schema, "DifficultyName"));
        }

        private static string? findReplayHash(IRealmObjectBase score, RealmSchemaSnapshot schema)
        {
            if (DynamicRowAccess.Resolve(score, schema, "Files") is not System.Collections.IEnumerable files)
                return null;

            foreach (object? file in files)
            {
                if (file is not IRealmObjectBase usage)
                    continue;

                if (DynamicRowAccess.ResolveString(usage, schema, "Filename") is not string name
                    || !name.EndsWith(".osr", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return DynamicRowAccess.ResolveString(usage, schema, "File.Hash");
            }

            return null;
        }

        /// <summary>元数据是嵌入类，链接可能缺失；缺失按空值处理，由标题兜底文案接管。</summary>
        /// <summary>
        /// 元数据作者列是 <c>RealmUser</c> 链接，官方展示取的是 <c>Author.Username</c>；
        /// 直接读 <c>Metadata.Author</c> 得到的是对象，拼进标题只会得到空。
        /// </summary>
        private static string? metadataAuthor(IRealmObjectBase row, RealmSchemaSnapshot schema) =>
            DynamicRowAccess.ResolveString(row, schema, "Metadata.Author.Username");

        private static string? metadataField(IRealmObjectBase row, RealmSchemaSnapshot schema, string field) =>
            DynamicRowAccess.ResolveString(row, schema, $"Metadata.{field}");

        private static IRealmObjectBase? firstListElement(IRealmObjectBase row, RealmSchemaSnapshot schema, string listProperty)
        {
            if (DynamicRowAccess.Resolve(row, schema, listProperty) is not System.Collections.IEnumerable list)
                return null;

            foreach (object? element in list)
                return element as IRealmObjectBase;

            return null;
        }

        private static int listCount(IRealmObjectBase row, RealmSchemaSnapshot schema, string listProperty)
        {
            if (DynamicRowAccess.Resolve(row, schema, listProperty) is not System.Collections.ICollection collection)
                return 0;

            return collection.Count;
        }

        private static Guid readId(IRealmObjectBase row, RealmSchemaSnapshot schema) =>
            DynamicRowAccess.Resolve(row, schema, "ID") is Guid id ? id : Guid.Empty;

        private static void report(IProgress<ScanProgress>? progress, int index, int total, string? message)
        {
            progress?.Report(new ScanProgress
            {
                Progress = total == 0 ? 1 : (double)index / total,
                Message = message ?? string.Empty,
            });
        }
    }
}
