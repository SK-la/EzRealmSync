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

        /// <summary>
        /// 返回共享会话：数据/修复/导出共用同一 <see cref="RealmFileRegistry"/>（真实模式）。
        /// </summary>
        public static RealmServiceSession CreateSession(bool uiTestMode, MockEzRealmSyncOptions? mockOptions = null)
        {
            if (uiTestMode)
            {
                var mock = new MockEzRealmSyncService(mockOptions ?? new MockEzRealmSyncOptions());
                return new RealmServiceSession(mock, mock, mock, mock);
            }

#if HAS_EZ_OSU_GAME
            var data = new RealmRealmDataService();
            return new RealmServiceSession(data, data, data, new RealmEzRealmSyncService());
#else
            return new RealmServiceSession(
                new StubRealmDataService(),
                new StubRealmFixExportService(),
                new StubRealmFixExportService(),
                new StubRealmEzRealmSyncService());
#endif
        }

        public static IRealmDataService CreateDataService(bool uiTestMode, MockEzRealmSyncOptions? mockOptions = null) => CreateSession(uiTestMode, mockOptions).Data;

        public static IRealmFixService CreateFixService(bool uiTestMode, MockEzRealmSyncOptions? mockOptions = null) => CreateSession(uiTestMode, mockOptions).Fix;

        public static IRealmExportService CreateExportService(bool uiTestMode, MockEzRealmSyncOptions? mockOptions = null) => CreateSession(uiTestMode, mockOptions).Export;
    }

    public sealed class RealmServiceSession
    {
        public RealmServiceSession(
            IRealmDataService data,
            IRealmFixService fix,
            IRealmExportService export,
            IEzRealmSyncService sync)
        {
            Data = data;
            Fix = fix;
            Export = export;
            Sync = sync;
        }

        public IRealmDataService Data { get; }

        public IRealmFixService Fix { get; }

        public IRealmExportService Export { get; }

        public IEzRealmSyncService Sync { get; }
    }
}
