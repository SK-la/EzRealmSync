namespace osu.EzRealmSync.Desktop
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            ApplicationThemeManager.Apply(ApplicationTheme.Dark);
            base.OnStartup(e);
        }
    }
}
