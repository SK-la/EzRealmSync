#if HAS_EZ_OSU_GAME
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.EzRealmSync.Models;
using osu.Game.Scoring;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 从已打开的 Realm 构建导出目录（不依赖浏览快照）。
    /// </summary>
    internal static class RealmExportCatalogBuilder
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
                        addScores(realm, items, progress, cancellationToken);
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
                                .Where(b => b.BeatmapSet == null || !b.BeatmapSet.DeletePending)
                                .AsEnumerable()
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

        private static void addScores(RealmInstance realm, List<RealmExportItem> items, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
        {
            int index = 0;
            var scores = realm.All<ScoreInfo>().Where(s => !s.DeletePending).AsEnumerable().ToList();

            foreach (var score in scores)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var replay = score.Files.FirstOrDefault(f => f.Filename.EndsWith(".osr", StringComparison.OrdinalIgnoreCase));
                if (replay == null)
                    continue;

                var entry = RealmExportExecutor.CreateScoreEntry(score, groupScoresByPlayer: true);
                string player = score.RealmUser.Username;

                report(progress, ++index, scores.Count, score.GetDisplayString());

                items.Add(new RealmExportItem
                {
                    Id = score.ID,
                    Title = score.GetDisplayString(),
                    Artist = score.BeatmapInfo?.Metadata.Artist ?? score.BeatmapHash,
                    PlayerName = player,
                    RelativePath = entry.SourceRelative,
                    DestinationRelativePath = entry.DestinationRelative,
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
