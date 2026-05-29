using osu.EzRealmSync.AppModel.Localization;

namespace osu.EzRealmSync.Desktop.Services
{
    internal static class WpfUiDialogService
    {
        public static Task<string?> PickFolderAsync(Window owner, string? initialPath)
        {
            return Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var dialog = new FolderPickerWindow(initialPath, Loc.Get("FolderPickerTitle"))
                {
                    Owner = owner,
                };

                return dialog.ShowDialog() == true ? dialog.SelectedPath : null;
            }).Task;
        }

        public static Task<bool> ConfirmAsync(Window owner, string message, string title, bool dangerous)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
                return Application.Current.Dispatcher.Invoke(() => ConfirmAsync(owner, message, title, dangerous));

            return confirmOnUiThreadAsync(owner, message, title, dangerous);
        }

        private static async Task<bool> confirmOnUiThreadAsync(Window owner, string message, string title, bool dangerous)
        {
            var box = new UiMessageBox
            {
                Title = title,
                Content = new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 0),
                },
                PrimaryButtonText = Loc.Get("Yes"),
                CloseButtonText = Loc.Get("No"),
                PrimaryButtonAppearance = dangerous ? ControlAppearance.Caution : ControlAppearance.Primary,
                Owner = owner,
            };

            var result = await box.ShowDialogAsync();
            return result == UiMessageBoxResult.Primary;
        }
    }
}
