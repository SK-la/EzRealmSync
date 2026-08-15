#if HAS_EZ_OSU_GAME
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.EzRealmSync.IO;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 在不迁移 schema 的前提下，把 Realm 收藏夹与 osu!stable <c>collection.db</c> 互转。
    /// 导入按名称合并 MD5（与游戏 <c>LegacyCollectionImporter</c> 一致）。
    /// </summary>
    internal static class RealmCollectionDbSync
    {
        public static int Export(RealmAccess access, IReadOnlyCollection<Guid> selectedIds, string outputFile)
        {
            var idSet = selectedIds as HashSet<Guid> ?? selectedIds.ToHashSet();
            var entries = new List<LegacyCollectionDbEntry>();

            access.Run(realm =>
            {
                foreach (var collection in realm.All<BeatmapCollection>())
                {
                    if (idSet.Count > 0 && !idSet.Contains(collection.ID))
                        continue;

                    entries.Add(new LegacyCollectionDbEntry(
                        collection.Name,
                        collection.BeatmapMD5Hashes.ToList()));
                }
            });

            LegacyCollectionDb.WriteFile(outputFile, entries);
            return entries.Count;
        }

        public static RealmCollectionDbImportResult Import(RealmAccess access, IReadOnlyList<LegacyCollectionDbEntry> collections)
        {
            int created = 0;
            int merged = 0;
            int addedHashes = 0;

            access.Write(realm =>
            {
                foreach (var incoming in collections)
                {
                    var existing = realm.All<BeatmapCollection>().FirstOrDefault(c => c.Name == incoming.Name);

                    if (existing == null)
                    {
                        var collection = new BeatmapCollection(incoming.Name);
                        foreach (string hash in incoming.BeatmapMd5Hashes)
                            collection.BeatmapMD5Hashes.Add(hash);

                        realm.Add(collection);
                        created++;
                        addedHashes += incoming.BeatmapMd5Hashes.Count;
                        continue;
                    }

                    int added = 0;

                    foreach (string hash in incoming.BeatmapMd5Hashes)
                    {
                        if (existing.BeatmapMD5Hashes.Contains(hash))
                            continue;

                        existing.BeatmapMD5Hashes.Add(hash);
                        added++;
                    }

                    existing.LastModified = DateTimeOffset.UtcNow;
                    merged++;
                    addedHashes += added;
                }
            });

            return new RealmCollectionDbImportResult
            {
                CollectionCount = collections.Count,
                CreatedCount = created,
                MergedCount = merged,
                AddedHashCount = addedHashes,
            };
        }
    }
}
#endif
