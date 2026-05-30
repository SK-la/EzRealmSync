#if HAS_EZ_OSU_GAME
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

        public void InvalidateSnapshotCache(string realmId) => snapshotCache.Remove(realmId);
    }
}
#endif
