using osu.EzRealmSync.AppModel;
using osu.EzRealmSync.AppModel.Localization;
using osu.EzRealmSync.Desktop.ViewModels;
using osu.Game.EzRealmSync;

namespace osu.EzRealmSync.Desktop
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var options = EzRealmSyncLaunchOptions.Parse(args);
            Loc.SetLanguage(AppLanguage.ZhHans);

            var syncService = EzRealmSyncServiceFactory.Create(options.UiTestMode, options.MockOptions);
            var dataService = EzRealmSyncServiceFactory.CreateDataService(options.UiTestMode, options.MockOptions);
            var fixService = EzRealmSyncServiceFactory.CreateFixService(options.UiTestMode, options.MockOptions);
            var exportService = EzRealmSyncServiceFactory.CreateExportService(options.UiTestMode, options.MockOptions);
            var presenter = new RealmAppPresenter(syncService, dataService, fixService, exportService, options);

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
