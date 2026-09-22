#if HAS_EZ_OSU_GAME
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.EzRealmSync.Models;
using osu.Game.Scoring;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Tests.TestInfrastructure
{
    /// <summary>
    /// 导出目录的 **typed 参考实现**，只给对照测试用（产品侧已改为动态读取 <c>DynamicExportCatalogBuilder</c>）。
    ///
    /// 与旧产品实现只有一处差别：筛选「未软删谱面」的查询先 <c>AsEnumerable()</c> 再 <c>Where</c>。
    /// 原来的 <c>realm.All&lt;BeatmapInfo&gt;().Where(b =&gt; b.BeatmapSet == null || !b.BeatmapSet.DeletePending)</c>
    /// 表达式 Realm 的 LINQ provider 翻译不了，枚举时抛 <c>NotSupportedException</c>；这里保留参考语义，
    /// 不在测试夹具里重演那个查询缺陷。
    /// </summary>
    internal static class TypedExportCatalogBuilder
    {
        public static RealmExportCatalog Build(RealmAccess access, ExportDataKind kind, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            var items = new List<RealmExportItem>();

            access.Run(realm =>
            {
                switch (kind)
                {
                    case ExportDataKind.BeatmapSet:
                        addBeatmapSets(realm, items, progress, cancellationToken);
                        break;

                    case ExportDataKind.Beatmap:
                        addBeatmaps(realm, items, progress, cancellationToken);
                        break;

                    case ExportDataKind.Collection:
                    case ExportDataKind.CollectionDb:
                        addCollections(realm, items, progress, cancellationToken);
                        break;

                    case ExportDataKind.Score:
                        addScores(realm, items, progress, cancellationToken, requireReplayFile: true);
                        break;

                    case ExportDataKind.ScoreDb:
                        addScores(realm, items, progress, cancellationToken, requireReplayFile: false);
                        break;
                }
            });

            progress?.Report(new ScanProgress { Progress = 1, Message = "列表加载完成" });
            return new RealmExportCatalog { Kind = kind, Items = items };
        }

        private static void addBeatmapSets(RealmInstance realm, List<RealmExportItem> items, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
        {
            int index = 0;
            var sets = realm.All<BeatmapSetInfo>().Where(s => !s.DeletePending).AsEnumerable().ToList();

            foreach (var set in sets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var beatmap = set.Beatmaps.FirstOrDefault();
                if (beatmap == null)
                    continue;

                string path = RealmFilePathHelper.GetStoragePath(beatmap.Hash);
                report(progress, ++index, sets.Count, set.Metadata.GetDisplayString());

                items.Add(new RealmExportItem
                {
                    Id = set.ID,
                    Title = set.Metadata.Title,
                    Artist = set.Metadata.Artist,
                    RelativePath = path,
                });
            }
        }

        private static void addBeatmaps(RealmInstance realm, List<RealmExportItem> items, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
        {
            int index = 0;
            var beatmaps = realm.All<BeatmapInfo>()
                                .AsEnumerable()
                                .Where(b => b.BeatmapSet == null || !b.BeatmapSet.DeletePending)
                                .ToList();

            foreach (var beatmap in beatmaps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                report(progress, ++index, beatmaps.Count, beatmap.Metadata.GetDisplayString());

                items.Add(new RealmExportItem
                {
                    Id = beatmap.ID,
                    Title = beatmap.Metadata.Title,
                    Artist = beatmap.Metadata.Artist,
                    RelativePath = RealmFilePathHelper.GetStoragePath(beatmap.Hash),
                });
            }
        }

        private static void addCollections(RealmInstance realm, List<RealmExportItem> items, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
        {
            int collectionIndex = 0;
            var collections = realm.All<BeatmapCollection>().AsEnumerable().ToList();

            foreach (var collection in collections)
            {
                cancellationToken.ThrowIfCancellationRequested();
                collectionIndex++;

                int count = collection.BeatmapMD5Hashes.Count;
                report(progress, collectionIndex, collections.Count, collection.Name);

                items.Add(new RealmExportItem
                {
                    Id = collection.ID,
                    Title = collection.Name,
                    Artist = string.Empty,
                    CollectionName = collection.Name,
                    BeatmapCount = count,
                });
            }
        }

        private static void addScores(RealmInstance realm, List<RealmExportItem> items, IProgress<ScanProgress>? progress, CancellationToken cancellationToken, bool requireReplayFile)
        {
            int index = 0;
            var scores = realm.All<ScoreInfo>().Where(s => !s.DeletePending).AsEnumerable().ToList();

            foreach (var score in scores)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (requireReplayFile)
                {
                    var replay = score.Files.FirstOrDefault(f => f.Filename.EndsWith(".osr", StringComparison.OrdinalIgnoreCase));
                    if (replay == null)
                        continue;
                }

                // scores.db 仅支持官方四模式；列表仍展示全部，导出时再跳过非 legacy。
                string player = score.RealmUser.Username;
                string relative = string.Empty;
                string? dest = null;

                if (requireReplayFile)
                {
                    var entry = TypedExportExecutor.CreateScoreEntry(score, groupScoresByPlayer: true);
                    relative = entry.SourceRelative;
                    dest = entry.DestinationRelative;
                }

                report(progress, ++index, scores.Count, score.GetDisplayString());

                items.Add(new RealmExportItem
                {
                    Id = score.ID,
                    Title = score.GetDisplayString(),
                    Artist = score.BeatmapInfo?.Metadata.Artist ?? score.BeatmapHash,
                    PlayerName = player,
                    RelativePath = relative,
                    DestinationRelativePath = dest,
                });
            }
        }

        private static void report(IProgress<ScanProgress>? progress, int index, int total, string message)
        {
            progress?.Report(new ScanProgress
            {
                Progress = total == 0 ? 1 : (double)index / total,
                Message = message,
            });
        }
    }
}
#endif
