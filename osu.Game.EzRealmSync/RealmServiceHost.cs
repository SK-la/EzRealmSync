using osu.Game.EzRealmSync.Abstractions;
using osu.Game.EzRealmSync.Mock;

namespace osu.Game.EzRealmSync
{
    /// <summary>
    /// 持有当前 <see cref="RealmServiceSession"/>，支持运行时切换 UI 测试 / 真实后端。
    /// </summary>
    public sealed class RealmServiceHost
    {
        private readonly MockEzRealmSyncOptions mockOptions;
        private RealmServiceSession session = null!;

        public RealmServiceHost(bool uiTestMode, MockEzRealmSyncOptions? mockOptions = null)
        {
            this.mockOptions = mockOptions ?? new MockEzRealmSyncOptions();
            SetUiTestMode(uiTestMode, force: true);
        }

        public bool UiTestMode { get; private set; }

        public EzRealmSyncBackendKind BackendKind { get; private set; }

        public IEzRealmSyncService Sync => session.Sync;

        public IRealmDataService Data => session.Data;

        public IRealmFixService Fix => session.Fix;

        public IRealmExportService Export => session.Export;

        public void SetUiTestMode(bool uiTestMode, bool force = false)
        {
            if (!force && UiTestMode == uiTestMode)
                return;

            UiTestMode = uiTestMode;
            session = EzRealmSyncServiceFactory.CreateSession(uiTestMode, mockOptions);
            BackendKind = EzRealmSyncBackend.Detect(session.Data);
        }
    }
}
