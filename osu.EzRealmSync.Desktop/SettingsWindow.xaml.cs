using osu.EzRealmSync.AppModel;
using osu.EzRealmSync.AppModel.Localization;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.Desktop
{
    public partial class SettingsWindow : FluentWindow
    {
        private readonly SyncPresenter presenter;
        private readonly EzRealmSyncLaunchOptions options;

        public SettingsWindow(SyncPresenter presenter, EzRealmSyncLaunchOptions options)
        {
            this.presenter = presenter;
            this.options = options;
            InitializeComponent();
            ApplicationThemeManager.Apply(this);
            Loaded += (_, _) => refreshUi();
        }

        private void refreshUi()
        {
            Title = Loc.Get("SettingsTitle");
            SettingsTitleBar.Title = Loc.Get("SettingsTitle");
            TitleLabel.Text = Loc.Get("SettingsTitle");
            LanguageLabel.Text = Loc.Get("Language");
            UiTestCheck.Content = Loc.Get("UiTestMode");
            UiTestHint.Text = Loc.Get("UiTestModeHint");
            CloseButton.Content = Loc.Get("Close");
            UiTestCheck.IsChecked = presenter.UiTestMode.Value;

            LanguageCombo.Items.Clear();
            LanguageCombo.Items.Add(new ComboBoxItem { Content = Loc.Get("LanguageZh"), Tag = AppLanguage.ZhHans });
            LanguageCombo.Items.Add(new ComboBoxItem { Content = Loc.Get("LanguageEn"), Tag = AppLanguage.En });
            LanguageCombo.SelectedIndex = Loc.CurrentLanguage == AppLanguage.En ? 1 : 0;

            bool showMock = options.UiTestMode && presenter.MockService != null;
            MockOptionsLabel.Visibility = showMock ? Visibility.Visible : Visibility.Collapsed;
            DatasetLabel.Visibility = showMock ? Visibility.Visible : Visibility.Collapsed;
            DatasetCombo.Visibility = showMock ? Visibility.Visible : Visibility.Collapsed;
            ErrorInjectionLabel.Visibility = showMock ? Visibility.Visible : Visibility.Collapsed;
            ErrorInjectionCombo.Visibility = showMock ? Visibility.Visible : Visibility.Collapsed;

            if (!showMock || presenter.MockService == null)
                return;

            MockOptionsLabel.Text = Loc.Get("MockOptions");
            DatasetLabel.Text = Loc.Get("MockDataset");
            ErrorInjectionLabel.Text = Loc.Get("MockErrorInjection");

            DatasetCombo.Items.Clear();
            foreach (MockDatasetSize size in Enum.GetValues<MockDatasetSize>())
                DatasetCombo.Items.Add(new ComboBoxItem { Content = size.ToString(), Tag = size });

            ErrorInjectionCombo.Items.Clear();
            foreach (MockErrorInjection injection in Enum.GetValues<MockErrorInjection>())
                ErrorInjectionCombo.Items.Add(new ComboBoxItem { Content = injection.ToString(), Tag = injection });

            DatasetCombo.SelectedItem = findItem(DatasetCombo, presenter.MockService.Options.DatasetSize);
            ErrorInjectionCombo.SelectedItem = findItem(ErrorInjectionCombo, presenter.MockService.Options.ErrorInjection);
        }

        private static ComboBoxItem? findItem(ComboBox combo, object value)
        {
            foreach (var item in combo.Items)
            {
                if (item is ComboBoxItem { Tag: var tag } && tag.Equals(value))
                    return (ComboBoxItem)item;
            }

            return null;
        }

        private void LanguageCombo_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageCombo.SelectedItem is ComboBoxItem { Tag: AppLanguage language })
                Loc.SetLanguage(language);
        }

        private void DatasetCombo_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (presenter.MockService != null && DatasetCombo.SelectedItem is ComboBoxItem { Tag: MockDatasetSize size })
                presenter.MockService.Options.DatasetSize = size;
        }

        private void ErrorInjectionCombo_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (presenter.MockService != null && ErrorInjectionCombo.SelectedItem is ComboBoxItem { Tag: MockErrorInjection injection })
                presenter.MockService.Options.ErrorInjection = injection;
        }

        private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();
    }
}
