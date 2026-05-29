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

            var service = EzRealmSyncServiceFactory.Create(options.UiTestMode, options.MockOptions);
            var presenter = new SyncPresenter(service, options);

            var app = new App();
            app.InitializeComponent();

            var mainWindow = new MainWindow
            {
                DataContext = new MainViewModel(presenter, options),
            };

            app.Run(mainWindow);
        }
    }
}
