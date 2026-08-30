#if HAS_EZ_OSU_GAME
using osu.Framework.Platform;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 从已打开的 <see cref="RealmAccess"/> 读取 Diff 快照。
    /// </summary>
    public static class RealmDiffReader
    {
        public static RealmDiffSnapshot Read(RealmAccess access, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            RealmDiffSnapshot? snapshot = null;

            access.Run(realm =>
            {
                progress?.Report(new ScanProgress { Progress = 0, Message = "正在读取谱面集…" });
                cancellationToken.ThrowIfCancellationRequested();

                var entities = new List<RealmDiffEntity>();
                entities.AddRange(readBeatmapSets(realm));
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new ScanProgress { Progress = 0.4, Message = "正在读取难度…" });
                entities.AddRange(readBeatmaps(realm));
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new ScanProgress { Progress = 0.65, Message = "正在读取成绩…" });
                entities.AddRange(readScores(realm));
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new ScanProgress { Progress = 0.85, Message = "正在读取收藏夹…" });
                entities.AddRange(readCollections(realm));

                snapshot = new RealmDiffSnapshot { Entities = entities };
            });

            progress?.Report(new ScanProgress { Progress = 1, Message = "读取完成" });
            return snapshot ?? new RealmDiffSnapshot();
        }

        public static RealmAccess OpenEzRealm(string realmFilePath, int pinnedDiskSchemaVersion) => open(realmFilePath, ez: true, pinnedDiskSchemaVersion);

        public static RealmAccess OpenOfficialRealm(string realmFilePath, int pinnedDiskSchemaVersion) => open(realmFilePath, ez: false, pinnedDiskSchemaVersion);

        private static RealmAccess open(string realmFilePath, bool ez, int pinnedDiskSchemaVersion)
        {
            string fullPath = Path.GetFullPath(realmFilePath);
            string storageRoot = RealmWorkspacePaths.ResolveStorageRoot(fullPath);
            string filename = RealmWorkspacePaths.ResolveStorageRelativeRealmPath(fullPath);
            var storage = new NativeStorage(storageRoot);

            return ez
                ? RealmAccess.OpenWithoutMigration(storage, filename, pinnedDiskSchemaVersion)
                : OfficialRealmAccess.OpenWithoutMigration(storage, filename, pinnedDiskSchemaVersion);
        }

        private static IEnumerable<RealmDiffEntity> readBeatmapSets(RealmInstance realm)
        {
            foreach (var set in realm.LiveBeatmapSets())
            {
                var metadata = set.Beatmaps.FirstOrDefault()?.Metadata;
                yield return new RealmDiffEntity
                {
                    Id = set.ID,
                    EntityKind = EntityKind.BeatmapSet,
                    Hash = set.Hash,
                    Title = metadata?.Title ?? string.Empty,
                    Artist = metadata?.Artist ?? string.Empty,
                    OnlineId = set.OnlineID,
                };
            }
        }

        private static IEnumerable<RealmDiffEntity> readBeatmaps(RealmInstance realm)
        {
            foreach (var beatmap in realm.LiveBeatmaps())
            {
                yield return new RealmDiffEntity
                {
                    Id = beatmap.ID,
                    EntityKind = EntityKind.Beatmap,
                    Hash = beatmap.Hash,
                    Title = beatmap.Metadata.Title,
                    Artist = beatmap.Metadata.Artist,
                    Ruleset = beatmap.Ruleset.ShortName,
                    DifficultyName = beatmap.DifficultyName,
                };
            }
        }

        private static IEnumerable<RealmDiffEntity> readScores(RealmInstance realm)
        {
            foreach (var score in realm.LiveScores())
            {
                yield return new RealmDiffEntity
                {
                    Id = score.ID,
                    EntityKind = EntityKind.Score,
                    Hash = score.Hash,
                    Title = score.BeatmapInfo?.Metadata.Title ?? score.BeatmapHash,
                    Artist = score.BeatmapInfo?.Metadata.Artist ?? string.Empty,
                    Ruleset = score.Ruleset.ShortName,
                    Date = score.Date,
                };
            }
        }

        private static IEnumerable<RealmDiffEntity> readCollections(RealmInstance realm)
        {
            foreach (var collection in realm.All<BeatmapCollection>().AsEnumerable())
            {
                yield return new RealmDiffEntity
                {
                    Id = collection.ID,
                    EntityKind = EntityKind.BeatmapCollection,
                    Title = collection.Name,
                    CollectionBeatmapCount = collection.BeatmapMD5Hashes.Count,
                    CollectionHashFingerprint = fingerprintHashes(collection.BeatmapMD5Hashes),
                };
            }
        }

        private static string fingerprintHashes(IList<string> hashes)
        {
            if (hashes.Count == 0)
                return string.Empty;

            string[] sorted = hashes.OrderBy(h => h, StringComparer.Ordinal).ToArray();
            return string.Join("|", sorted);
        }
    }
}
#endif
