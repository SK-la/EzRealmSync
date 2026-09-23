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

        /// <summary>
        /// 按<b>名称列</b>合并 MD5（与游戏导入 collection.db 的口径一致）。
        ///
        /// 主键是哪一列由磁盘 schema 决定，本方法不依赖它：官方 51/52 的收藏夹主键是 <c>ID</c>（Guid），
        /// 更老的库是 <c>Name</c>（string），两种都按 <c>Name</c> 找同名收藏夹；
        /// 新建时按主键类型给值（Guid 主键得自带一个新 ID，否则会撞上 <c>Guid.Empty</c>）。
        /// </summary>
        public static RealmCollectionDbImportResult Import(DynamicRealmSession session, RealmSchemaSnapshot schema, IReadOnlyList<LegacyCollectionDbEntry> collections)
        {
            int created = 0;
            int merged = 0;
            int addedHashes = 0;

            if (!schema.TryFindClass(OfficialBaselineSchema.BeatmapCollection, out RealmClassSchema? classSchema)
                || !classSchema.TryFindProperty("Name", out _)
                || classSchema.PrimaryKeyProperty is not { } primaryKey
                || !classSchema.TryFindProperty(primaryKey, out RealmPropertySchema? keyProperty))
            {
                throw new InvalidOperationException(
                    $"磁盘 schema 里 {OfficialBaselineSchema.BeatmapCollection} 缺 Name 列或主键，无法按名称合并（schema {session.DiskSchemaVersion}）。");
            }

            bool guidPrimaryKey = keyProperty.Type.HasFlag(PropertyType.Guid);

            using (var transaction = session.Realm.BeginWrite())
            {
                foreach (var incoming in collections)
                {
                    IRealmObjectBase? existing = findByName(session, schema, incoming.Name);

                    if (existing == null)
                    {
                        IRealmObjectBase collection = guidPrimaryKey
                            ? DynamicRealmAccess.Create(session.Realm, OfficialBaselineSchema.BeatmapCollection, Guid.NewGuid())
                            : DynamicRealmAccess.Create(session.Realm, OfficialBaselineSchema.BeatmapCollection, incoming.Name);

                        DynamicRealmAccess.Set(collection, OfficialBaselineSchema.BeatmapCollection, "Name", incoming.Name);

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

                RealmSchemaDriftGuard.EnsureUnchanged(schema, DynamicSchemaReader.Read(session.Realm), session.FilePath);
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

        /// <summary>按 <c>Name</c> 找已有收藏夹：不假定它是主键，也不假定它非空。</summary>
        private static IRealmObjectBase? findByName(DynamicRealmSession session, RealmSchemaSnapshot schema, string name)
        {
            foreach (IRealmObjectBase row in DynamicRowAccess.AllRows(session, schema, OfficialBaselineSchema.BeatmapCollection))
            {
                if (string.Equals(DynamicRowAccess.ResolveString(row, schema, "Name") ?? string.Empty, name, StringComparison.Ordinal))
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
