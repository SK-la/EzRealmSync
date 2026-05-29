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

            var session = EzRealmSyncServiceFactory.CreateSession(options.UiTestMode, options.MockOptions);
            var presenter = new RealmAppPresenter(session.Sync, session.Data, session.Fix, session.Export, options);

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
