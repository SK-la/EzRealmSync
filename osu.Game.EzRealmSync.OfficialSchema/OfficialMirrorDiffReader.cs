using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.OfficialSchema.V51;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.OfficialSchema
{
    /// <summary>官方镜像只读 → Diff 实体 DTO。</summary>
    public static class OfficialMirrorDiffReader
    {
        public static RealmReadResult Read(RealmReadJob job)
        {
            try
            {
                using var realm = OfficialMirrorRealm.OpenPinned(job.RealmFilePath, job.PinnedDiskSchemaVersion, readOnly: true);
                var kinds = parseEntityKinds(job.EntityKinds);
                var entities = enumerate(realm).Where(e => kinds.Count == 0 || kinds.Contains(e.EntityKind)).ToList();

                return new RealmReadResult
                {
                    Success = true,
                    Entities = entities,
                };
            }
            catch (Exception ex)
            {
                return new RealmReadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                };
            }
        }

        private static IEnumerable<RealmDiffEntityDto> enumerate(RealmInstance realm)
        {
            foreach (var set in realm.All<BeatmapSetInfo>().AsEnumerable().Where(s => !s.DeletePending))
            {
                var metadata = set.Beatmaps.FirstOrDefault()?.Metadata;
                yield return new RealmDiffEntityDto
                {
                    Id = set.ID,
                    EntityKind = "BeatmapSet",
                    Hash = set.Hash,
                    Title = metadata?.Title ?? string.Empty,
                    Artist = metadata?.Artist ?? string.Empty,
                    OnlineId = set.OnlineID,
                };
            }

            foreach (var beatmap in realm.All<BeatmapInfo>().AsEnumerable().Where(b => !b.Hidden && b.BeatmapSet != null && !b.BeatmapSet.DeletePending))
            {
                yield return new RealmDiffEntityDto
                {
                    Id = beatmap.ID,
                    EntityKind = "Beatmap",
                    Hash = beatmap.Hash,
                    Title = beatmap.Metadata.Title,
                    Artist = beatmap.Metadata.Artist,
                    Ruleset = beatmap.Ruleset.ShortName,
                    DifficultyName = beatmap.DifficultyName,
                };
            }

            foreach (var score in realm.All<ScoreInfo>().AsEnumerable().Where(s => !s.DeletePending))
            {
                yield return new RealmDiffEntityDto
                {
                    Id = score.ID,
                    EntityKind = "Score",
                    Hash = score.Hash,
                    Title = score.BeatmapInfo?.Metadata.Title ?? score.BeatmapHash,
                    Artist = score.BeatmapInfo?.Metadata.Artist ?? string.Empty,
                    Ruleset = score.Ruleset.ShortName,
                    Date = score.Date,
                };
            }

            foreach (var collection in realm.All<BeatmapCollection>().AsEnumerable())
            {
                yield return new RealmDiffEntityDto
                {
                    Id = collection.ID,
                    EntityKind = "BeatmapCollection",
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

        private static HashSet<string> parseEntityKinds(IReadOnlyList<string> kinds)
        {
            if (kinds.Count == 0)
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            return kinds.Where(k => !string.IsNullOrWhiteSpace(k)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }
}
