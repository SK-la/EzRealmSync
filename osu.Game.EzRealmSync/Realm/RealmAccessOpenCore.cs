#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm.Readers;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 进程内打开 current schema 的共享逻辑（ReadDiff / ReadBrowse / Sidecar 对齐）。
    /// Legacy schema 不在此打开，由 Sidecar + reader 包负责。
    /// </summary>
    internal static class RealmAccessOpenCore
    {
        public static RealmReaderRoute ResolveRoute(int pinnedDiskSchemaVersion) =>
            RealmReaderRegistry.Instance.Router.ResolveRoute(pinnedDiskSchemaVersion);

        public static bool IsCurrentRoute(RealmReaderRoute route) =>
            route is RealmReaderRoute.OfficialCurrent or RealmReaderRoute.EzCurrent;

        public static bool RequiresSidecar(int pinnedDiskSchemaVersion) =>
            !IsCurrentRoute(ResolveRoute(pinnedDiskSchemaVersion));

        public static RealmAccess OpenCurrentInProcess(string realmFilePath, int pinnedDiskSchemaVersion, RealmReaderRoute route)
        {
            return route switch
            {
                RealmReaderRoute.OfficialCurrent => RealmDiffReader.OpenOfficialRealm(realmFilePath, pinnedDiskSchemaVersion),
                RealmReaderRoute.EzCurrent => RealmDiffReader.OpenEzRealm(realmFilePath, pinnedDiskSchemaVersion),
                _ => throw new InvalidOperationException(
                    $"schema {pinnedDiskSchemaVersion} 非 bundled lib 当前版本，不能进程内打开：{realmFilePath}"),
            };
        }

        public static bool TryOpenCurrentInProcess(string realmFilePath, int pinnedDiskSchemaVersion, out RealmAccess? access)
        {
            access = null;
            var route = ResolveRoute(pinnedDiskSchemaVersion);

            if (!IsCurrentRoute(route))
            {
                EzRealmSyncLog.Debug(
                    $"In-process open skipped (legacy route {route}) schema={pinnedDiskSchemaVersion} file={realmFilePath}");
                return false;
            }

            try
            {
                access = OpenCurrentInProcess(realmFilePath, pinnedDiskSchemaVersion, route);
                access.Run(_ => { });
                EzRealmSyncLog.Debug($"In-process open OK route={route} schema={pinnedDiskSchemaVersion} file={realmFilePath}");
                return true;
            }
            catch (Exception ex)
            {
                access?.Dispose();
                access = null;

                throw new InvalidOperationException(
                    $"无法用 bundled lib 进程内打开当前 schema {pinnedDiskSchemaVersion}（route={route}）：{ExceptionFormatting.SafeFormat(ex)}",
                    ex);
            }
        }
    }
}
#endif
