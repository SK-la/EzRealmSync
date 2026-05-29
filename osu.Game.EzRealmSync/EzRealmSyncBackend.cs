using osu.Game.EzRealmSync.Abstractions;
using osu.Game.EzRealmSync.Mock;
using osu.Game.EzRealmSync.Realm;

namespace osu.Game.EzRealmSync
{
    /// <summary>当前进程实际使用的数据后端（由工厂在启动时选定，运行时不可切换）。</summary>
    public enum EzRealmSyncBackendKind
    {
        /// <summary>Mock 假数据（<c>--ui-test</c>）。</summary>
        Mock,

        /// <summary>已链接 lib/osu.Game.dll，可读写真实 Realm。</summary>
        Real,

        /// <summary>未放置 lib，占位 Stub（加载会失败或列表为空）。</summary>
        Stub,
    }

    public static class EzRealmSyncBackend
    {
        public static EzRealmSyncBackendKind Detect(IRealmDataService dataService) => dataService.GetType().Name switch
        {
            nameof(MockEzRealmSyncService) => EzRealmSyncBackendKind.Mock,
            "RealmRealmDataService" => EzRealmSyncBackendKind.Real,
            nameof(StubRealmDataService) => EzRealmSyncBackendKind.Stub,
            _ => EzRealmSyncBackendKind.Stub,
        };

        public static bool IsOsuGameDllOnDisk => ResolveRuntimeLibDirectory() != null;

        /// <summary>当前运行的 <c>osu.Game.EzRealmSync.dll</c> 是否在编译时启用了 <c>HAS_EZ_OSU_GAME</c>。</summary>
        public static bool IsRealBackendCompiled => typeof(EzRealmSyncBackend).Assembly.GetType("osu.Game.EzRealmSync.Realm.RealmRealmDataService") != null;

        /// <summary>
        /// 运行时 Ez osu.Game 依赖目录：优先 <c>{exe}/lib</c>，其次 exe 根目录（旧平铺），再向上查找开发仓库 layout。
        /// </summary>
        public static string? ResolveRuntimeLibDirectory()
        {
            string exeLib = Path.Combine(AppContext.BaseDirectory, "lib");
            if (File.Exists(Path.Combine(exeLib, "osu.Game.dll")))
                return exeLib;

            if (File.Exists(Path.Combine(AppContext.BaseDirectory, "osu.Game.dll")))
                return AppContext.BaseDirectory;

            return findDevLibDirectory();
        }

        private static string? findDevLibDirectory()
        {
            string? dir = AppContext.BaseDirectory;

            for (int i = 0; i < 6 && dir != null; i++)
            {
                string candidate = Path.Combine(dir, "lib", "osu.Game.dll");
                if (File.Exists(candidate))
                    return Path.GetDirectoryName(candidate)!;

                dir = Directory.GetParent(dir)?.FullName;
            }

            return null;
        }
    }
}
