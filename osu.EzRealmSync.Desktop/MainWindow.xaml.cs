using System.ComponentModel;
using osu.EzRealmSync.AppModel;
using osu.EzRealmSync.AppModel.Localization;
using osu.EzRealmSync.Desktop.Services;
using osu.EzRealmSync.Desktop.ViewModels;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.Desktop
{
    public partial class MainWindow
    {
        private ShellViewModel? vm;
        private WorkspacePageProvider? pageProvider;
        private string? lastSnackbarStatus;
        private bool suppressLanguageChange;
        private bool suppressNavChange;
        private int settingsPopupOpenDropdownCount;

        public MainWindow()
        {
            InitializeComponent();
            ApplicationThemeManager.Apply(this);
            Loaded += onLoaded;
        }

        private void onLoaded(object sender, RoutedEventArgs e)
        {
            wireContentDialogHitTest();
            wireSettingsPopupComboBoxes();
            WpfUiServices.Attach(RootContentDialogHost, RootSnackbarPresenter);

            if (DataContext is not ShellViewModel shell)
                return;

            vm = shell;
            pageProvider = new WorkspacePageProvider();
            pageProvider.Attach(shell);
            WorkspaceNav.SetPageProviderService(pageProvider);

            wireViewModel(shell);
            refreshChrome();
            refreshSettingsFlyout();
            navigateToTab(MainWorkspaceTab.Import);

            Loc.LanguageChanged += () => Dispatcher.Invoke(() =>
            {
                refreshChrome();
                refreshSettingsFlyout();
            });
        }

        private void wireContentDialogHitTest()
        {
            void syncHitTest() => RootContentDialogHost.IsHitTestVisible = RootContentDialogHost.Content != null;

            RootContentDialogHost.IsHitTestVisible = false;
            DependencyPropertyDescriptor
                .FromProperty(ContentProperty, typeof(ContentControl))
                .AddValueChanged(RootContentDialogHost, (_, _) => syncHitTest());
            syncHitTest();
        }

        /// <summary>
        /// ComboBox dropdowns are separate popups; keep the settings flyout open while any is expanded.
        /// </summary>
        private void wireSettingsPopupComboBoxes()
        {
            foreach (ComboBox combo in new[] { ActiveReaderPackageCombo, DatasetCombo, ErrorInjectionCombo })
            {
                combo.DropDownOpened += (_, _) =>
                {
                    settingsPopupOpenDropdownCount++;
                    SettingsPopup.StaysOpen = true;
                };
                combo.DropDownClosed += (_, _) =>
                {
                    settingsPopupOpenDropdownCount = Math.Max(0, settingsPopupOpenDropdownCount - 1);
                    if (settingsPopupOpenDropdownCount == 0)
                        SettingsPopup.StaysOpen = false;
                };
            }
        }

        private void wireViewModel(ShellViewModel shell)
        {
            shell.PropertyChanged += (_, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    switch (e.PropertyName)
                    {
                        case nameof(ShellViewModel.WindowTitle):
                            Title = shell.WindowTitle;
                            MainTitleBar.Title = shell.WindowTitle;
                            break;
                        case nameof(ShellViewModel.StatusMessage):
                            updateStatus(shell.StatusMessage);
                            break;
                        case nameof(ShellViewModel.Progress):
                            StatusProgressRing.Progress = shell.Progress;
                            StatusProgressRing.IsIndeterminate = shell is { IsBusy: true, Progress: <= 0 };
                            break;
                        case nameof(ShellViewModel.CurrentTab):
                            navigateToTab(shell.CurrentTab);
                            break;
                        case nameof(ShellViewModel.IsBusy):
                            UiTestModeSwitch.IsEnabled = !shell.IsBusy;
                            ConfirmDeleteSwitch.IsEnabled = !shell.IsBusy;
                            if (!shell.IsBusy)
                                lastSnackbarStatus = null;
                            break;
                        case nameof(ShellViewModel.ConfirmBeforeDelete):
                            ConfirmDeleteSwitch.IsChecked = shell.ConfirmBeforeDelete;
                            break;
                        case nameof(ShellViewModel.CanUseFixAndExport):
                            updateFixExportNavEnabled();
                            break;
                    }
                });
            };

            shell.Presenter.UiTestMode.BindValueChanged(_ => Dispatcher.Invoke(() => UiTestModeSwitch.IsChecked = shell.Presenter.UiTestMode.Value));
            shell.Presenter.WorkspaceCapabilitiesChanged += () => Dispatcher.Invoke(updateFixExportNavEnabled);
            updateFixExportNavEnabled();
        }

        private void updateFixExportNavEnabled()
        {
            if (vm == null)
                return;

            bool enabled = vm.CanUseFixAndExport;
            NavFix.IsEnabled = enabled;
            NavExport.IsEnabled = enabled;
        }

        private void WorkspaceNav_OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            if (suppressNavChange || vm == null)
                return;

            if (WorkspaceNav.SelectedItem is NavigationViewItem { TargetPageType: { } pageType })
                vm.CurrentTab = WorkspacePageProvider.TabForPageType(pageType);
        }

        private void navigateToTab(MainWorkspaceTab tab)
        {
            suppressNavChange = true;
            WorkspaceNav.Navigate(WorkspacePageProvider.PageTypeForTab(tab));
            suppressNavChange = false;
        }

        private void refreshChrome()
        {
            if (vm == null)
                return;

            Title = vm.WindowTitle;
            MainTitleBar.Title = vm.WindowTitle;
            NavImport.Content = Loc.Get("TabImport");
            NavData.Content = Loc.Get("TabData");
            NavSync.Content = Loc.Get("TabSync");
            NavFix.Content = Loc.Get("TabFix");
            NavExport.Content = Loc.Get("TabExport");
            updateFixExportNavEnabled();
            refreshLanguageCombo();
            updateStatus(vm.StatusMessage);
            StatusProgressRing.Progress = vm.Progress;
            StatusProgressRing.IsIndeterminate = vm.IsBusy && vm.Progress <= 0;
            UiTestModeSwitch.IsEnabled = !vm.IsBusy;
        }

        private void refreshLanguageCombo()
        {
            suppressLanguageChange = true;

            LanguageLabel.Text = Loc.Get("Language");
            LanguageCombo.Items.Clear();
            LanguageCombo.Items.Add(new ComboBoxItem { Content = Loc.Get("LanguageZh"), Tag = AppLanguage.ZhHans });
            LanguageCombo.Items.Add(new ComboBoxItem { Content = Loc.Get("LanguageEn"), Tag = AppLanguage.En });
            LanguageCombo.SelectedIndex = Loc.CurrentLanguage == AppLanguage.En ? 1 : 0;

            suppressLanguageChange = false;
        }

        private void refreshSettingsFlyout()
        {
            if (vm == null)
                return;

            SettingsButton.Content = Loc.Get("Settings");
            GeneralSettingsExpander.Header = Loc.Get("SettingsGeneral");
            UiTestModeSwitch.Content = Loc.Get("UiTestMode");
            UiTestHint.Text = Loc.Get("UiTestModeHint");
            UiTestModeSwitch.IsChecked = vm.Presenter.UiTestMode.Value;
            UiTestModeSwitch.IsEnabled = !vm.IsBusy;

            ConfirmDeleteSwitch.Content = Loc.Get("ConfirmBeforeDelete");
            ConfirmDeleteHint.Text = Loc.Get("ConfirmBeforeDeleteHint");
            ConfirmDeleteSwitch.IsChecked = vm.ConfirmBeforeDelete;
            ConfirmDeleteSwitch.IsEnabled = !vm.IsBusy;

            ReaderSettingsExpander.Header = Loc.Get("ReaderSettings");
            ReaderSettingsExpander.Visibility = vm.Presenter.UiTestMode.Value ? Visibility.Collapsed : Visibility.Visible;
            ReaderPackagesPathLabel.Text = Loc.Get("ReaderPackagesDirectory");
            ReaderPackagesPathText.Text = vm.Presenter.ReaderPackagesPath;
            ActiveReaderPackageLabel.Text = Loc.Get("ActiveReaderPackage");
            ReaderRestartHint.Text = Loc.Get("ReaderRestartHint");
            ActiveReaderPackageCombo.IsEnabled = !vm.IsBusy;

            ActiveReaderPackageCombo.Items.Clear();
            ActiveReaderPackageCombo.Items.Add(new ComboBoxItem
            {
                Content = Loc.Get("ReaderPackageDefault"),
                Tag = null,
            });

            foreach (var package in vm.Presenter.InstalledReaderPackages)
            {
                string schemaSummary = package.DiskSchemaVersions.Count == 0
                    ? string.Empty
                    : string.Join(", ", package.DiskSchemaVersions);
                string suffix = package.HasValidLib ? string.Empty : Loc.Get("ReaderPackageMissingLib");
                ActiveReaderPackageCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"{package.DisplayName} ({schemaSummary}){suffix}",
                    Tag = package.Id,
                });
            }

            string? activePackageId = vm.Presenter.ActiveReaderPackageId.Value;
            ActiveReaderPackageCombo.SelectedItem = ActiveReaderPackageCombo.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => (item.Tag as string) == activePackageId)
                ?? ActiveReaderPackageCombo.Items[0];

            bool showMock = vm.Presenter.UiTestMode.Value && vm.Presenter.MockService != null;
            MockSettingsExpander.Visibility = showMock ? Visibility.Visible : Visibility.Collapsed;

            if (!showMock || vm.Presenter.MockService == null)
                return;

            MockSettingsExpander.Header = Loc.Get("MockOptions");
            DatasetLabel.Text = Loc.Get("MockDataset");
            ErrorInjectionLabel.Text = Loc.Get("MockErrorInjection");

            DatasetCombo.Items.Clear();
            foreach (MockDatasetSize size in Enum.GetValues<MockDatasetSize>())
                DatasetCombo.Items.Add(new ComboBoxItem { Content = size.ToString(), Tag = size });

            ErrorInjectionCombo.Items.Clear();
            foreach (MockErrorInjection injection in Enum.GetValues<MockErrorInjection>())
                ErrorInjectionCombo.Items.Add(new ComboBoxItem { Content = injection.ToString(), Tag = injection });

            DatasetCombo.SelectedItem = findComboItem(DatasetCombo, vm.Presenter.MockService.Options.DatasetSize);
            ErrorInjectionCombo.SelectedItem = findComboItem(ErrorInjectionCombo, vm.Presenter.MockService.Options.ErrorInjection);
        }

        private static ComboBoxItem? findComboItem(ComboBox combo, object value)
        {
            foreach (object? item in combo.Items)
            {
                if (item is ComboBoxItem { Tag: var tag } boxItem && tag.Equals(value))
                    return boxItem;
            }

            return null;
        }

        private void LanguageCombo_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressLanguageChange)
                return;

            if (LanguageCombo.SelectedItem is ComboBoxItem { Tag: AppLanguage language })
                Loc.SetLanguage(language);
        }

        private void SettingsButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (SettingsPopup.IsOpen)
            {
                SettingsPopup.IsOpen = false;
                return;
            }

            settingsPopupOpenDropdownCount = 0;
            SettingsPopup.StaysOpen = false;
            SettingsPopup.IsOpen = true;
            refreshSettingsFlyout();
        }

        private void UiTestModeSwitch_OnClick(object sender, RoutedEventArgs e)
        {
            if (vm == null)
                return;

            bool enabled = UiTestModeSwitch.IsChecked == true;
            if (vm.Presenter.UiTestMode.Value == enabled)
                return;

            vm.Presenter.UiTestMode.Value = enabled;
            refreshSettingsFlyout();
        }

        private void ConfirmDeleteSwitch_OnClick(object sender, RoutedEventArgs e)
        {
            if (vm == null)
                return;

            vm.ConfirmBeforeDelete = ConfirmDeleteSwitch.IsChecked == true;
        }

        private void ActiveReaderPackageCombo_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (vm == null)
                return;

            if (ActiveReaderPackageCombo.SelectedItem is ComboBoxItem { Tag: string id })
                vm.Presenter.ActiveReaderPackageId.Value = id;
            else
                vm.Presenter.ActiveReaderPackageId.Value = null;
        }

        private void DatasetCombo_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (vm?.Presenter.MockService != null && DatasetCombo.SelectedItem is ComboBoxItem { Tag: MockDatasetSize size })
                vm.Presenter.MockService.Options.DatasetSize = size;
        }

        private void ErrorInjectionCombo_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (vm?.Presenter.MockService != null && ErrorInjectionCombo.SelectedItem is ComboBoxItem { Tag: MockErrorInjection injection })
                vm.Presenter.MockService.Options.ErrorInjection = injection;
        }

        private void updateStatus(string message)
        {
            StatusText.Text = message;
            StatusText.ToolTip = string.IsNullOrWhiteSpace(message) ? null : message;

            if (vm == null || vm.IsBusy || string.IsNullOrWhiteSpace(message) || message == lastSnackbarStatus || isTransientStatus(message))
                return;

            lastSnackbarStatus = message;
            WpfUiSnackbar.Show(vm.WindowTitle, message, ControlAppearance.Info);
        }

        private static bool isTransientStatus(string message) => message.Contains("扫描", StringComparison.Ordinal) ||
                                                                 message.Contains("Scanning", StringComparison.OrdinalIgnoreCase) ||
                                                                 message.Contains("计算", StringComparison.Ordinal) ||
                                                                 message.Contains("Computing", StringComparison.OrdinalIgnoreCase);
    }
}
