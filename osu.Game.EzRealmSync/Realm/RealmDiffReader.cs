#if HAS_EZ_OSU_GAME
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;
using osu.Game.Scoring;
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

                progress?.Report(new ScanProgress { Progress = 0.7, Message = "正在读取成绩…" });
                entities.AddRange(readScores(realm));

                snapshot = new RealmDiffSnapshot { Entities = entities };
            });

            progress?.Report(new ScanProgress { Progress = 1, Message = "读取完成" });
            return snapshot ?? new RealmDiffSnapshot();
        }

        public static RealmAccess OpenEzRealm(string realmFilePath) => open(realmFilePath, ez: true);

        public static RealmAccess OpenOfficialRealm(string realmFilePath) => open(realmFilePath, ez: false);

        private static RealmAccess open(string realmFilePath, bool ez)
        {
            string fullPath = Path.GetFullPath(realmFilePath);
            string storageRoot = RealmWorkspacePaths.ResolveStorageRoot(fullPath);
            string filename = Path.GetFileName(fullPath);
            var storage = new NativeStorage(storageRoot);

            return ez
                ? new RealmAccess(storage, filename)
                : new OfficialRealmAccess(storage, filename);
        }

        private static IEnumerable<RealmDiffEntity> readBeatmapSets(RealmInstance realm)
        {
            foreach (var set in realm.All<BeatmapSetInfo>().Where(s => !s.DeletePending))
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
            foreach (var beatmap in realm.All<BeatmapInfo>())
            {
                if (beatmap.BeatmapSet?.DeletePending == true)
                    continue;

                yield return new RealmDiffEntity
                {
                    Id = beatmap.ID,
                    EntityKind = EntityKind.Beatmap,
                    Hash = beatmap.Hash,
                    Title = beatmap.Metadata.Title,
                    Artist = beatmap.Metadata.Artist,
                    Ruleset = beatmap.Ruleset?.ShortName ?? string.Empty,
                    DifficultyName = beatmap.DifficultyName,
                };
            }
        }

        private static IEnumerable<RealmDiffEntity> readScores(RealmInstance realm)
        {
            foreach (var score in realm.All<ScoreInfo>().Where(s => !s.DeletePending))
            {
                yield return new RealmDiffEntity
                {
                    Id = score.ID,
                    EntityKind = EntityKind.Score,
                    Hash = score.Hash,
                    Title = score.BeatmapInfo?.Metadata.Title ?? score.BeatmapHash,
                    Artist = score.BeatmapInfo?.Metadata.Artist ?? string.Empty,
                    Ruleset = score.Ruleset?.ShortName ?? string.Empty,
                    Date = score.Date,
                };
            }
        }
    }
}
#endif
