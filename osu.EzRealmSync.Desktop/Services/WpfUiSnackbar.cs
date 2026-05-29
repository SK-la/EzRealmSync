namespace osu.EzRealmSync.Desktop.Services
{
    /// <summary>
    /// 主窗 <see cref="SnackbarPresenter"/> 的轻量封装；需在 MainWindow 加载后调用 <see cref="Attach"/>。
    /// </summary>
    internal static class WpfUiSnackbar
    {
        private static ISnackbarService? service;

        public static void Attach(SnackbarPresenter presenter)
        {
            service = new SnackbarService();
            service.SetSnackbarPresenter(presenter);
        }

        public static void Show(string title, string message, ControlAppearance appearance = ControlAppearance.Secondary)
        {
            service?.Show(title, message, appearance);
        }
    }
}
