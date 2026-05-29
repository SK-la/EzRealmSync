namespace osu.Game.EzRealmSync.Models
{
    /// <summary>
    /// 兼容入口：请优先使用 <see cref="RealmWritePlan.TryFromEndpoints"/>。
    /// </summary>
    public static class RealmSyncDirectionHelper
    {
        public static bool TryResolveWritePlan(
            RealmFileEntry endpointA,
            RealmFileEntry endpointB,
            out SyncDirection direction,
            out PathConfiguration paths,
            out string? error)
        {
            direction = default;
            paths = new PathConfiguration();
            error = null;

            if (!RealmWritePlan.TryFromEndpoints(endpointA, endpointB, out var plan, out error) || plan == null)
                return false;

            direction = plan.LegacyDirection;
            paths = plan.ToLegacyPathConfiguration();
            return true;
        }

        public static bool TryInferDirection(RealmFileEntry source, RealmFileEntry target, out SyncDirection direction, out string? error) =>
            TryResolveWritePlan(source, target, out direction, out _, out error);

        public static PathConfiguration CreatePathConfiguration(RealmFileEntry endpointA, RealmFileEntry endpointB, SyncDirection direction) =>
            RealmWritePlan.TryFromEndpoints(endpointA, endpointB, out var plan, out _) && plan != null
                ? plan.ToLegacyPathConfiguration()
                : new PathConfiguration
                {
                    EzDataPath = RealmWorkspacePaths.ResolveStorageRoot(endpointA.FilePath),
                    OfficialDataPath = RealmWorkspacePaths.ResolveStorageRoot(endpointB.FilePath),
                };

        public static PathConfiguration CreatePaths(RealmFileEntry source, RealmFileEntry target, SyncDirection direction) =>
            CreatePathConfiguration(source, target, direction);
    }
}
