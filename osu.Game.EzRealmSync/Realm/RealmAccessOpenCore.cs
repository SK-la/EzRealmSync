#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Realm.Readers;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 进程内打开：仅 Ez current。官方任意版本禁止进程内打开（Ez 对象模型含 Ez 列）。
    /// Ez legacy 由 ReadSidecar；官方由 Official Worker（OfficialSchema）。
    /// </summary>
    internal static class RealmAccessOpenCore
    {
        public static RealmReaderRoute ResolveRoute(int pinnedDiskSchemaVersion)
        {
            return RealmReaderRegistry.Instance.Router.ResolveRoute(pinnedDiskSchemaVersion);
        }

        public static bool IsInProcessReadableRoute(RealmReaderRoute route) =>
            route == RealmReaderRoute.EzCurrent;

        public static bool RequiresOutOfProcessRead(int pinnedDiskSchemaVersion)
        {
            return !IsInProcessReadableRoute(ResolveRoute(pinnedDiskSchemaVersion));
        }

        /// <summary>兼容旧名：非 Ez current 即需子进程（官方 → Official Worker；Ez legacy → Sidecar）。</summary>
        public static bool RequiresSidecar(int pinnedDiskSchemaVersion) =>
            RequiresOutOfProcessRead(pinnedDiskSchemaVersion);

        public static RealmAccess OpenCurrentInProcess(string realmFilePath, int pinnedDiskSchemaVersion, RealmReaderRoute route)
        {
            return route switch
            {
                RealmReaderRoute.EzCurrent => RealmDiffReader.OpenEzRealm(realmFilePath, pinnedDiskSchemaVersion),
                _ => throw new InvalidOperationException(
                    $"schema {pinnedDiskSchemaVersion}（route={route}）不得进程内打开：官方走 Official Worker，Ez legacy 走 ReadSidecar。文件：{realmFilePath}")
            };
        }

        public static bool TryOpenCurrentInProcess(string realmFilePath, int pinnedDiskSchemaVersion, out RealmAccess? access)
        {
            access = null;
            RealmReaderRoute route = ResolveRoute(pinnedDiskSchemaVersion);

            if (!IsInProcessReadableRoute(route))
            {
                EzRealmSyncLog.Debug(
                    $"In-process open skipped (route {route}) schema={pinnedDiskSchemaVersion} file={realmFilePath}");
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
                    $"无法用 bundled lib 进程内打开当前 Ez schema {pinnedDiskSchemaVersion}（route={route}）：{ExceptionFormatting.SafeFormat(ex)}",
                    ex);
            }
        }
    }
}
#endif
