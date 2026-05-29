using osu.EzRealmSync.AppModel;
using osu.EzRealmSync.AppModel.Localization;
using osu.EzRealmSync.Desktop.Services;
using osu.EzRealmSync.Desktop.ViewModels;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.Desktop
{
    public partial class MainWindow : FluentWindow
    {
        private MainViewModel vm = null!;
        private bool suppressDirectionChange;
        private string? lastSnackbarStatus;

        public MainWindow()
        {
            InitializeComponent();
            ApplicationThemeManager.Apply(this);
            Loaded += (_, _) =>
            {
                WpfUiServices.Attach(RootContentDialogHost, RootSnackbarPresenter);
                bindViewModel();
            };
        }

        private void bindViewModel()
        {
            if (DataContext is not MainViewModel viewModel)
                return;

            vm = viewModel;
            setupDataGridColumns();
            setupEntityFilterCombo();
            wirePresenterEvents();
            refreshAllUi();
        }

        private void setupDataGridColumns()
        {
            DiffGrid.Columns.Clear();
            var checkColumn = new DataGridCheckBoxColumn
            {
                Binding = new System.Windows.Data.Binding(nameof(DiffRowModel.IsSelected)) { UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged },
                Width = 40,
                ElementStyle = DiffGrid.CheckBoxColumnElementStyle,
                EditingElementStyle = DiffGrid.CheckBoxColumnEditingElementStyle,
            };
            DiffGrid.Columns.Add(checkColumn);
            DiffGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColTitle"), Binding = new System.Windows.Data.Binding(nameof(DiffRowModel.Title)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            DiffGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColArtist"), Binding = new System.Windows.Data.Binding(nameof(DiffRowModel.Artist)), Width = 120 });
            DiffGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColHash"), Binding = new System.Windows.Data.Binding(nameof(DiffRowModel.Hash)), Width = 100 });
            DiffGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColRuleset"), Binding = new System.Windows.Data.Binding(nameof(DiffRowModel.Ruleset)), Width = 80 });
            DiffGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColDate"), Binding = new System.Windows.Data.Binding(nameof(DiffRowModel.Date)), Width = 130 });
            DiffGrid.ItemsSource = vm.DiffRows;
        }

        private void setupEntityFilterCombo()
        {
            EntityFilterCombo.Items.Clear();

            foreach (var filter in vm.EntityFilters)
            {
                EntityFilterCombo.Items.Add(new ComboBoxItem
                {
                    Content = vm.GetEntityFilterLabel(filter),
                    Tag = filter,
                });
            }

            EntityFilterCombo.SelectedIndex = 0;
        }

        private void wirePresenterEvents()
        {
            vm.PropertyChanged += (_, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    switch (e.PropertyName)
                    {
                        case nameof(MainViewModel.EndpointAPath):
                            if (PathA.Text != vm.EndpointAPath) PathA.Text = vm.EndpointAPath;
                            break;
                        case nameof(MainViewModel.EndpointBPath):
                            if (PathB.Text != vm.EndpointBPath) PathB.Text = vm.EndpointBPath;
                            break;
                        case nameof(MainViewModel.StatusMessage):
                            updateStatus(vm.StatusMessage);
                            break;
                        case nameof(MainViewModel.Progress):
                            ScanProgressRing.Progress = vm.Progress;
                            ScanProgressRing.IsIndeterminate = vm.IsBusy && vm.Progress <= 0;
                            break;
                        case nameof(MainViewModel.SelectionCountText):
                            SelectionCountText.Text = vm.SelectionCountText;
                            break;
                        case nameof(MainViewModel.IsBusy):
                            if (!vm.IsBusy)
                                lastSnackbarStatus = null;
                            updateActionButtons();
                            break;
                        case nameof(MainViewModel.CanApply):
                            updateActionButtons();
                            break;
                        case nameof(MainViewModel.Direction):
                            syncDirectionRadio();
                            break;
                        case nameof(MainViewModel.DeleteButtonText):
                        case nameof(MainViewModel.ApplyButtonText):
                        case nameof(MainViewModel.TabSourceOnlyLabel):
                        case nameof(MainViewModel.TabTargetOnlyLabel):
                        case nameof(MainViewModel.TabConflictedLabel):
                        case nameof(MainViewModel.SelectAllButtonText):
                        case nameof(MainViewModel.WindowTitle):
                            refreshLabels();
                            break;
                        case nameof(MainViewModel.CurrentCategory):
                            updateCategoryTabs();
                            break;
                    }
                });
            };

            PathA.TextChanged += (_, _) => vm.EndpointAPath = PathA.Text;
            PathB.TextChanged += (_, _) => vm.EndpointBPath = PathB.Text;
        }

        private void refreshAllUi()
        {
            Title = vm.WindowTitle;
            PathA.Text = vm.EndpointAPath;
            PathB.Text = vm.EndpointBPath;
            updateStatus(vm.StatusMessage);
            ScanProgressRing.Progress = vm.Progress;
            ScanProgressRing.IsIndeterminate = vm.IsBusy && vm.Progress <= 0;
            SelectionCountText.Text = vm.SelectionCountText;
            refreshLabels();
            syncDirectionRadio();
            updateActionButtons();
            updateCategoryTabs();
        }

        private void updateStatus(string message)
        {
            StatusText.Text = message;

            if (vm.IsBusy || string.IsNullOrWhiteSpace(message) || message == lastSnackbarStatus || isTransientStatus(message))
                return;

            lastSnackbarStatus = message;
            var appearance = inferStatusAppearance(message);
            WpfUiSnackbar.Show(vm.WindowTitle, message, appearance);
        }

        private static bool isTransientStatus(string message) =>
            message.Contains("扫描", StringComparison.Ordinal) ||
            message.Contains("Scanning", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("就绪", StringComparison.Ordinal) ||
            message.Contains("Ready", StringComparison.OrdinalIgnoreCase);

        private static ControlAppearance inferStatusAppearance(string message)
        {
            if (message.Contains('\n'))
                return ControlAppearance.Caution;

            var lower = message.ToLowerInvariant();
            if (lower.Contains("error") || lower.Contains("fail") || lower.Contains("错误") || lower.Contains("失败"))
                return ControlAppearance.Danger;

            if (lower.Contains("complete") || lower.Contains("ready") || lower.Contains("完成") || lower.Contains("就绪"))
                return ControlAppearance.Success;

            return ControlAppearance.Info;
        }

        private void refreshLabels()
        {
            SettingsButton.Content = vm.LocSettings;
            LabelEndpointA.Text = vm.LocEndpointA;
            LabelEndpointB.Text = vm.LocEndpointB;
            BrowseAButton.Content = vm.LocBrowse;
            BrowseBButton.Content = vm.LocBrowse;
            ScanButton.Content = vm.LocScanDiff;
            LabelSyncDirection.Text = vm.LocSyncDirection;
            DirectionAToB.Content = vm.LocDirectionAToB;
            DirectionBToA.Content = vm.LocDirectionBToA;
            LabelEntityFilter.Text = vm.LocEntityFilter;
            CollectionsPhase2Text.Text = vm.LocCollectionsPhase2;
            TabSourceOnly.Content = vm.TabSourceOnlyLabel;
            TabTargetOnly.Content = vm.TabTargetOnlyLabel;
            TabConflicted.Content = vm.TabConflictedLabel;
            SelectAllButton.Content = vm.SelectAllButtonText;
            ExportButton.Content = vm.LocExportOsr;
            DeleteButton.Content = vm.DeleteButtonText;
            ApplyButton.Content = vm.ApplyButtonText;

            for (int i = 0; i < EntityFilterCombo.Items.Count; i++)
            {
                if (EntityFilterCombo.Items[i] is ComboBoxItem item && item.Tag is EntityKindFilter filter)
                    item.Content = vm.GetEntityFilterLabel(filter);
            }
        }

        private void syncDirectionRadio()
        {
            suppressDirectionChange = true;
            DirectionAToB.IsChecked = vm.Direction == SyncDirection.EzToOfficial;
            DirectionBToA.IsChecked = vm.Direction == SyncDirection.OfficialToEz;
            suppressDirectionChange = false;
        }

        private void updateActionButtons()
        {
            ScanButton.IsEnabled = BrowseAButton.IsEnabled = BrowseBButton.IsEnabled = !vm.IsBusy;
            DeleteButton.IsEnabled = !vm.IsBusy;
            ApplyButton.IsEnabled = vm.CanApply && !vm.IsBusy;
            SelectAllButton.IsEnabled = !vm.IsBusy;
        }

        private void updateCategoryTabs()
        {
            TabSourceOnly.Appearance = vm.CurrentCategory == DiffCategory.SourceOnly ? ControlAppearance.Primary : ControlAppearance.Secondary;
            TabTargetOnly.Appearance = vm.CurrentCategory == DiffCategory.TargetOnly ? ControlAppearance.Primary : ControlAppearance.Secondary;
            TabConflicted.Appearance = vm.CurrentCategory == DiffCategory.Conflicted ? ControlAppearance.Primary : ControlAppearance.Secondary;
        }

        private void Direction_OnChecked(object sender, RoutedEventArgs e)
        {
            if (suppressDirectionChange || vm == null)
                return;

            if (DirectionAToB.IsChecked == true)
                vm.Direction = SyncDirection.EzToOfficial;
            else if (DirectionBToA.IsChecked == true)
                vm.Direction = SyncDirection.OfficialToEz;
        }

        private void EntityFilter_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EntityFilterCombo.SelectedItem is ComboBoxItem { Tag: EntityKindFilter filter })
                vm.EntityFilter = filter;
        }

        private void TabSourceOnly_OnClick(object sender, RoutedEventArgs e) => vm.CurrentCategory = DiffCategory.SourceOnly;
        private void TabTargetOnly_OnClick(object sender, RoutedEventArgs e) => vm.CurrentCategory = DiffCategory.TargetOnly;
        private void TabConflicted_OnClick(object sender, RoutedEventArgs e) => vm.CurrentCategory = DiffCategory.Conflicted;

        private async void BrowseA_OnClick(object sender, RoutedEventArgs e) => await vm.BrowseAAsync();
        private async void BrowseB_OnClick(object sender, RoutedEventArgs e) => await vm.BrowseBAsync();
        private void Scan_OnClick(object sender, RoutedEventArgs e) => vm.ScanCommand.Execute(null);
        private void Apply_OnClick(object sender, RoutedEventArgs e) => vm.ApplyCommand.Execute(null);
        private void Delete_OnClick(object sender, RoutedEventArgs e) => vm.DeleteCommand.Execute(null);
        private void SelectAll_OnClick(object sender, RoutedEventArgs e) => vm.SelectAllCommand.Execute(null);
        private void SettingsButton_OnClick(object sender, RoutedEventArgs e) => vm.OpenSettingsCommand.Execute(null);

        private void DiffGrid_OnCellChanged(object? sender, EventArgs e) => vm.OnDiffSelectionChanged();
    }
}
