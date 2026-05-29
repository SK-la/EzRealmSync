namespace osu.EzRealmSync.Desktop.Services
{
    /// <summary>
    /// <see cref="WpfUiServices"/> 通知的便捷封装。
    /// </summary>
    internal static class WpfUiSnackbar
    {
        public static void Show(string title, string message, ControlAppearance appearance = ControlAppearance.Secondary) => WpfUiServices.ShowSnackbar(title, message, appearance);
    }
}
