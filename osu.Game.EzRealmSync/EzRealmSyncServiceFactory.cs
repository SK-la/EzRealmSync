using osu.Game.EzRealmSync.Abstractions;
using osu.Game.EzRealmSync.Mock;
using osu.Game.EzRealmSync.Realm;

namespace osu.Game.EzRealmSync
{
    /// <summary>
    /// 按启动选项选择同步服务实现。UI 项目只调用本工厂，不直接引用 osu.Game 类型。
    /// </summary>
    public static class EzRealmSyncServiceFactory
    {
        public static IEzRealmSyncService Create(bool uiTestMode, MockEzRealmSyncOptions? mockOptions = null)
        {
            if (uiTestMode)
                return new MockEzRealmSyncService(mockOptions ?? new MockEzRealmSyncOptions());

#if HAS_EZ_OSU_GAME
            return new RealmEzRealmSyncService();
#else
            return new StubRealmEzRealmSyncService();
#endif
        }
    }
}
