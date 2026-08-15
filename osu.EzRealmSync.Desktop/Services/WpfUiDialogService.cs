using osu.EzRealmSync.AppModel.Localization;

namespace osu.EzRealmSync.Desktop.Services
{
    internal static class WpfUiDialogService
    {
        public static Task<string?> PickFolderAsync(Window owner, string? initialPath) => showPickerAsync(owner, PathPickerMode.Folder, initialPath, Loc.Get("FolderPickerTitle"));

        public static Task<string?> PickRealmPathAsync(Window owner, string? initialPath) => showPickerAsync(owner, PathPickerMode.RealmPath, initialPath, Loc.Get("PathPickerTitleRealm"));

        public static Task<string?> PickCollectionDbAsync(Window owner, string? initialPath) => showPickerAsync(owner, PathPickerMode.CollectionDb, initialPath, Loc.Get("PathPickerTitleCollectionDb"));

        private static Task<string?> showPickerAsync(Window owner, PathPickerMode mode, string? initialPath, string title)
        {
            return Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var dialog = new PathPickerWindow(mode, initialPath, title) { Owner = owner };
                return dialog.ShowDialog() == true ? dialog.SelectedPath : null;
            }).Task;
        }

        public static Task<bool> ConfirmAsync(Window owner, string message, string title, bool dangerous)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
                return Application.Current.Dispatcher.Invoke(() => ConfirmAsync(owner, message, title, dangerous));

            return confirmOnUiThreadAsync(message, title, dangerous);
        }

        private static async Task<bool> confirmOnUiThreadAsync(string message, string title, bool dangerous)
        {
            if (!WpfUiServices.IsReady)
            {
                var fallback = new UiMessageBox
                {
                    Title = title,
                    Content = message,
                    PrimaryButtonText = Loc.Get("Yes"),
                    CloseButtonText = Loc.Get("No"),
                    PrimaryButtonAppearance = dangerous ? ControlAppearance.Caution : ControlAppearance.Primary,
                    Owner = Application.Current.MainWindow,
                };

                return await fallback.ShowDialogAsync() == UiMessageBoxResult.Primary;
            }

            if (dangerous)
            {
                var dialog = new ContentDialog
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
                    PrimaryButtonAppearance = ControlAppearance.Caution,
                    DefaultButton = ContentDialogButton.Primary,
                };

                var result = await WpfUiServices.ContentDialogs.ShowAsync(dialog, CancellationToken.None);
                return result == ContentDialogResult.Primary;
            }

            var simpleResult = await WpfUiServices.ContentDialogs.ShowSimpleDialogAsync(
                new SimpleContentDialogCreateOptions
                {
                    Title = title,
                    Content = message,
                    PrimaryButtonText = Loc.Get("Yes"),
                    CloseButtonText = Loc.Get("No"),
                    DefaultButton = ContentDialogButton.Primary,
                },
                CancellationToken.None);

            return simpleResult == ContentDialogResult.Primary;
        }
    }
}
