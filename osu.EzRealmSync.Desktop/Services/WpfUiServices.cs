namespace osu.EzRealmSync.Desktop.Services
{
    /// <summary>
    /// 主窗加载后挂载的 WPF-UI 对话框 / 通知服务。
    /// </summary>
    internal static class WpfUiServices
    {
        private static IContentDialogService? contentDialogs;
        private static ISnackbarService? snackbars;

        public static bool IsReady => contentDialogs != null && snackbars != null;

        public static void Attach(ContentDialogHost dialogHost, SnackbarPresenter snackbarPresenter)
        {
            contentDialogs = new ContentDialogService();
            contentDialogs.SetDialogHost(dialogHost);

            snackbars = new SnackbarService();
            snackbars.SetSnackbarPresenter(snackbarPresenter);
        }

        public static IContentDialogService ContentDialogs => contentDialogs ?? throw new InvalidOperationException("WPF-UI services are not initialized. Call Attach from MainWindow first.");

        public static void ShowSnackbar(string title, string message, ControlAppearance appearance = ControlAppearance.Secondary)
        {
            if (snackbars == null)
                return;

            snackbars.Show(title, message, appearance);
        }
    }
}
