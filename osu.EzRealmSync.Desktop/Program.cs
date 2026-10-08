using osu.EzRealmSync.AppModel;
using osu.EzRealmSync.AppModel.Localization;
using osu.EzRealmSync.Desktop.ViewModels;
using osu.Game.EzRealmSync;
using osu.Game.EzRealmSync.Runtime;

namespace osu.EzRealmSync.Desktop
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            EzRealmSyncLog.Initialize();
            AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            {
                if (eventArgs.ExceptionObject is Exception ex)
                    EzRealmSyncLog.Exception(ex, "未处理异常");
                else
                    EzRealmSyncLog.Error($"未处理异常 {eventArgs.ExceptionObject}");
            };
            TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
            {
                EzRealmSyncLog.Exception(eventArgs.Exception, "未观察的任务异常");
                eventArgs.SetObserved();
            };

            EzRealmSyncRuntimeLibLoader.Install();

            var settings = AppSettingsStore.Load();

            EzRealmSyncLog.Info("EzRealmSync starting");

            var options = EzRealmSyncLaunchOptions.Parse(args);
            Loc.SetLanguage(AppLanguage.ZhHans);

            var serviceHost = new RealmServiceHost(options.UiTestMode, options.MockOptions);
            var presenter = new RealmAppPresenter(serviceHost, options);

            var app = new App();
            app.InitializeComponent();
            DesktopTheme.Apply(settings.DarkTheme);

            var mainWindow = new MainWindow
            {
                DataContext = new ShellViewModel(presenter, options),
            };

            app.Run(mainWindow);
        }
    }
}
