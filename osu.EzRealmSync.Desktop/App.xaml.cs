using osu.Game.EzRealmSync;

namespace osu.EzRealmSync.Desktop
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += (_, args) => EzRealmSyncLog.Exception(args.Exception, "界面未处理异常");
            base.OnStartup(e);
        }
    }
}
