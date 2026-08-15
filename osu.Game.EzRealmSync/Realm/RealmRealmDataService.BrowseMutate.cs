#if HAS_EZ_OSU_GAME
using osu.Game.EzRealmSync.IO;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    public sealed partial class RealmRealmDataService
    {
        public Task<int> DeleteBrowseEntitiesAsync(
            string realmId,
            RealmObjectClass objectClass,
            IReadOnlyList<Guid> entityIds,
            CancellationToken cancellationToken = default) =>
            Task.Run(() => deleteBrowseCore(realmId, objectClass, entityIds, cancellationToken), cancellationToken);

        private int deleteBrowseCore(
            string realmId,
            RealmObjectClass objectClass,
            IReadOnlyList<Guid> entityIds,
            CancellationToken cancellationToken)
        {
            if (entityIds.Count == 0)
                return 0;

            if (!registry.TryGet(realmId, out var file))
                throw new InvalidOperationException($"未找到 Realm 文件：{realmId}");

            string? processBlock = RealmProcessGuard.TryGetBlockingProcessMessage();
            if (processBlock != null)
                throw new InvalidOperationException(processBlock);

            cancellationToken.ThrowIfCancellationRequested();

            using var access = RealmSchemaProbe.Open(file.FilePath, file.SchemaVersion);
            int deleted = RealmBrowseEntityMutator.Delete(access, objectClass, entityIds);

            if (deleted > 0)
            {
                snapshotCache.Remove(realmId);
                InvalidateCatalog(realmId);
            }

            return deleted;
        }

        public Task<RealmCollectionDbImportResult> ImportCollectionDbAsync(
            string realmId,
            string collectionDbPath,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.Run(() => importCollectionDbCore(realmId, collectionDbPath, progress, cancellationToken), cancellationToken);

        private RealmCollectionDbImportResult importCollectionDbCore(
            string realmId,
            string collectionDbPath,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (!registry.TryGet(realmId, out var file))
                throw new InvalidOperationException($"未找到 Realm 文件：{realmId}");

            string? processBlock = RealmProcessGuard.TryGetBlockingProcessMessage();
            if (processBlock != null)
                throw new InvalidOperationException(processBlock);

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ScanProgress { Progress = 0.1, Message = "正在读取 collection.db…" });

            var collections = LegacyCollectionDb.ReadFile(collectionDbPath);
            progress?.Report(new ScanProgress { Progress = 0.4, Message = $"正在合并 {collections.Count} 个收藏夹…" });

            using var access = RealmSchemaProbe.Open(file.FilePath, file.SchemaVersion);
            var result = RealmCollectionDbSync.Import(access, collections);

            snapshotCache.Remove(realmId);
            InvalidateCatalog(realmId);
            progress?.Report(new ScanProgress { Progress = 1, Message = "导入完成" });
            return result;
        }

        public void InvalidateSnapshotCache(string realmId) => snapshotCache.Remove(realmId);
    }
}
#endif
