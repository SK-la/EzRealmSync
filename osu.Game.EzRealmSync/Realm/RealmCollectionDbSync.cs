using System.Collections;
using osu.Game.EzRealmSync.IO;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm.Dynamic;
using Realms;
using Realms.Schema;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 在不迁移 schema 的前提下，把 Realm 收藏夹与 osu!stable <c>collection.db</c> 互转。
    /// 导入按名称合并 MD5（与游戏 <c>LegacyCollectionImporter</c> 一致）。
    ///
    /// 动态读写：只用 <c>Name</c> / <c>BeatmapMD5Hashes</c> / <c>LastModified</c> 三列。
    /// </summary>
    /// <remarks>
    /// TODO(legacy-db-merge): 支持把收藏夹合并进磁盘上已有的 collection.db（当前仅支持导入到 Realm，以及写出新文件）。
    /// </remarks>
    internal static class RealmCollectionDbSync
    {
        public static int Export(DynamicRealmSession session, RealmSchemaSnapshot schema, IReadOnlyCollection<Guid> selectedIds, string outputFile)
        {
            var idSet = selectedIds as HashSet<Guid> ?? selectedIds.ToHashSet();
            var entries = new List<LegacyCollectionDbEntry>();

            foreach (IRealmObjectBase collection in DynamicRowAccess.AllRows(session, schema, OfficialBaselineSchema.BeatmapCollection))
            {
                if (idSet.Count > 0 && (DynamicRowAccess.Resolve(collection, schema, "ID") is not Guid id || !idSet.Contains(id)))
                    continue;

                entries.Add(new LegacyCollectionDbEntry(
                    DynamicRowAccess.ResolveString(collection, schema, "Name") ?? string.Empty,
                    readHashes(collection, schema).ToList()));
            }

            LegacyCollectionDb.WriteFile(outputFile, entries);
            return entries.Count;
        }

        public static RealmCollectionDbImportResult Import(DynamicRealmSession session, RealmSchemaSnapshot schema, IReadOnlyList<LegacyCollectionDbEntry> collections)
        {
            int created = 0;
            int merged = 0;
            int addedHashes = 0;

            string? primaryKey = schema.TryFindClass(OfficialBaselineSchema.BeatmapCollection, out RealmClassSchema? classSchema)
                                 && classSchema.PrimaryKeyProperty is { } key
                                 && classSchema.TryFindProperty(key, out RealmPropertySchema? keyProperty)
                                 && keyProperty.Type == PropertyType.String
                ? key
                : null;

            if (primaryKey == null)
            {
                throw new InvalidOperationException(
                    $"磁盘 schema 里 {OfficialBaselineSchema.BeatmapCollection} 的收藏夹不是以字符串主键（如 Name）标识，无法按名称合并（schema {session.DiskSchemaVersion}）。");
            }

            using (var transaction = session.Realm.BeginWrite())
            {
                foreach (var incoming in collections)
                {
                    IRealmObjectBase? existing = findByPrimaryKey(session, schema, primaryKey, incoming.Name);

                    if (existing == null)
                    {
                        IRealmObjectBase collection = DynamicRealmAccess.Create(session.Realm, OfficialBaselineSchema.BeatmapCollection, incoming.Name);

                        object? list = DynamicRealmAccess.GetListRaw(collection, "BeatmapMD5Hashes");
                        foreach (string hash in incoming.BeatmapMd5Hashes)
                            DynamicRealmAccess.AddToList(list, hash);

                        created++;
                        addedHashes += incoming.BeatmapMd5Hashes.Count;
                        continue;
                    }

                    int added = 0;

                    var existingHashes = new HashSet<string>(readHashes(existing, schema), StringComparer.Ordinal);

                    foreach (string hash in incoming.BeatmapMd5Hashes)
                    {
                        if (!existingHashes.Add(hash))
                            continue;

                        DynamicRealmAccess.AddToList(DynamicRealmAccess.GetListRaw(existing, "BeatmapMD5Hashes"), hash);
                        added++;
                    }

                    // 与原 typed 实现一致：合并过就刷新 LastModified（即使这次没有新增 MD5）。
                    DynamicRealmAccess.Set(existing, OfficialBaselineSchema.BeatmapCollection, "LastModified", DateTimeOffset.UtcNow);

                    merged++;
                    addedHashes += added;
                }

                transaction.Commit();
            }

            return new RealmCollectionDbImportResult
            {
                CollectionCount = collections.Count,
                CreatedCount = created,
                MergedCount = merged,
                AddedHashCount = addedHashes,
            };
        }

        /// <summary>按主键查已有收藏夹：主键列名从 schema 读，不假定一定是 <c>Name</c>。</summary>
        private static IRealmObjectBase? findByPrimaryKey(DynamicRealmSession session, RealmSchemaSnapshot schema, string primaryKey, string name)
        {
            foreach (IRealmObjectBase row in DynamicRowAccess.AllRows(session, schema, OfficialBaselineSchema.BeatmapCollection))
            {
                if (string.Equals(DynamicRowAccess.ResolveString(row, schema, primaryKey), name, StringComparison.Ordinal))
                    return row;
            }

            return null;
        }

        private static IEnumerable<string> readHashes(IRealmObjectBase collection, RealmSchemaSnapshot schema)
        {
            if (DynamicRowAccess.Resolve(collection, schema, "BeatmapMD5Hashes") is not IEnumerable hashes)
                yield break;

            foreach (object? hash in hashes)
            {
                if (hash is string value)
                    yield return value;
            }
        }
    }
}
