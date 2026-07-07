using osu.EzRealmSync.AppModel;
using osu.EzRealmSync.AppModel.Localization;
using osu.EzRealmSync.Desktop.ViewModels;
using osu.Game.EzRealmSync;
using osu.Game.EzRealmSync.Realm.Readers;
using osu.Game.EzRealmSync.Runtime;

namespace osu.EzRealmSync.Desktop
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var settings = AppSettingsStore.Load();
            var readerPackages = RealmReaderPackageCatalog.Scan(settings.ReaderPackagesDirectory);
            string? readerLibOverride = RealmReaderPackageCatalog.FindById(readerPackages, settings.ActiveReaderPackageId)?.LibDirectory;

            EzRealmSyncRuntimeLibLoader.Install(readerLibOverride);

            if (EzRealmSyncBackend.IsRealBackendCompiled)
                RealmReaderRegistry.Instance.Initialize(settings.ReaderPackagesDirectory);

            var options = EzRealmSyncLaunchOptions.Parse(args);
            Loc.SetLanguage(AppLanguage.ZhHans);

            var serviceHost = new RealmServiceHost(options.UiTestMode, options.MockOptions);
            var presenter = new RealmAppPresenter(serviceHost, options);

            var app = new App();
            app.InitializeComponent();

            var mainWindow = new MainWindow
            {
                DataContext = new ShellViewModel(presenter, options),
            };

            app.Run(mainWindow);
        }
    }
}
