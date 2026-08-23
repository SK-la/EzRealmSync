using System.Windows.Data;
using osu.EzRealmSync.AppModel;
using osu.EzRealmSync.AppModel.Localization;
using osu.EzRealmSync.Desktop.Helpers;
using osu.EzRealmSync.Desktop.ViewModels;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.Desktop.Pages
{
    public partial class SyncPage : UserControl
    {
        private ShellViewModel? vm;
        private bool syncGridBehaviorConfigured;

        public SyncPage()
        {
            InitializeComponent();
            Loaded += (_, _) => bindIfReady();
            DataContextChanged += (_, _) => bindIfReady();
        }

        private void bindIfReady()
        {
            if (DataContext is not ShellViewModel shell)
                return;

            vm = shell;
            refreshLabels();
            setupCombos();
            setupSyncGrid();
            configureSyncGridBehavior();
            refreshRealmCombos();
            SyncGrid.ItemsSource = vm.SyncRows;
            SelectionText.Text = vm.SelectionCountText;

            vm.PropertyChanged += (_, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    switch (e.PropertyName)
                    {
                        case nameof(ShellViewModel.RealmFiles):
                        case nameof(ShellViewModel.SyncRealmIdA):
                        case nameof(ShellViewModel.SyncRealmIdB):
                            refreshRealmCombos();
                            break;
                        case nameof(ShellViewModel.SelectionCountText):
                            SelectionText.Text = vm!.SelectionCountText;
                            break;
                        case nameof(ShellViewModel.CurrentCategory):
                            updateCategoryTabs();
                            break;
                        case nameof(ShellViewModel.IsBusy):
                            updateButtons();
                            break;
                    }
                });
            };

            vm.Presenter.SyncRowsChanged += () => Dispatcher.Invoke(() =>
            {
                DataGridColumnFilterHelper.ResetFilters(SyncGrid);
                SyncGrid.ItemsSource = vm.SyncRows;
            });
            vm.Presenter.LabelsChanged += () => Dispatcher.Invoke(refreshTabLabels);

            updateCategoryTabs();
            updateButtons();
        }

        private void refreshLabels()
        {
            EndpointALabel.Text = Loc.Get("EndpointA");
            EndpointBLabel.Text = Loc.Get("EndpointB");
            SetOpLabel.Text = Loc.Get("SetOperation");
            ActionLabel.Text = Loc.Get("SyncAction");
            WriteTargetLabel.Text = Loc.Get("SyncWriteTarget");
            ComputeButton.Content = Loc.Get("ComputeSet");
            ExecuteButton.Content = Loc.Get("ExecuteAction");
            refreshTabLabels();
            refreshTooltips();
        }

        private void refreshTabLabels()
        {
            if (vm == null)
                return;

            TabSourceOnly.Content = Loc.Get("TabOnlyInA");
            TabTargetOnly.Content = Loc.Get("TabOnlyInB");
            TabConflicted.Content = Loc.Get("TabConflicted");
            SelectAllButton.Content = Loc.Get("SelectAll");
            refreshTooltips();
        }

        private void refreshTooltips()
        {
            TabSourceOnly.ToolTip = Loc.Get("TooltipTabOnlyInA");
            TabTargetOnly.ToolTip = Loc.Get("TooltipTabOnlyInB");
            TabConflicted.ToolTip = Loc.Get("TooltipTabConflicted");
            SelectAllButton.ToolTip = Loc.Get("TooltipSelectAll");
            ComputeButton.ToolTip = Loc.Get("TooltipComputeSet");
            ExecuteButton.ToolTip = Loc.Get("TooltipExecuteAction");
            WriteTargetCombo.ToolTip = Loc.Get("TooltipSyncWriteTarget");
            ActionCombo.ToolTip = Loc.Get("TooltipSyncAction");
        }

        private void setupCombos()
        {
            if (SetOpCombo.Items.Count > 0)
                return;

            foreach (var op in vm!.SetOperations)
                SetOpCombo.Items.Add(new ComboBoxItem { Content = vm.GetSetOperationLabel(op), Tag = op });

            foreach (var action in vm.SyncActions)
                ActionCombo.Items.Add(new ComboBoxItem { Content = vm.GetSyncActionLabel(action), Tag = action });

            foreach (var endpoint in Enum.GetValues<SyncWriteEndpoint>())
                WriteTargetCombo.Items.Add(new ComboBoxItem { Content = vm.GetSyncWriteEndpointLabel(endpoint), Tag = endpoint });

            EntityFilterCombo.Items.Clear();
            foreach (var filter in vm.EntityFilters)
                EntityFilterCombo.Items.Add(new ComboBoxItem { Content = vm.GetEntityFilterLabel(filter), Tag = filter });

            SetOpCombo.SelectedIndex = 2;
            ActionCombo.SelectedIndex = 0;
            WriteTargetCombo.SelectedIndex = vm.SyncWriteTarget == SyncWriteEndpoint.A ? 0 : 1;
            EntityFilterCombo.SelectedIndex = 0;
        }

        private void setupSyncGrid()
        {
            if (SyncGrid.Columns.Count > 0)
                return;

            SyncGrid.Columns.Add(DataGridCheckColumnHelper.CreateColumn());

            addTextColumn(Loc.Get("ColKind"), nameof(DiffRowModel.EntityKind), 80);
            addTextColumn(Loc.Get("ColTitle"), nameof(DiffRowModel.Title), new DataGridLength(1, DataGridLengthUnitType.Star));
            addTextColumn(Loc.Get("ColArtist"), nameof(DiffRowModel.Artist), 110);
            addTextColumn(Loc.Get("ColHash"), nameof(DiffRowModel.Hash), 100);
            addTextColumn(Loc.Get("ColRuleset"), nameof(DiffRowModel.Ruleset), 72);
            addTextColumn(Loc.Get("ColDate"), nameof(DiffRowModel.Date), 120);
        }

        private void addTextColumn(string header, string path, double width) => addTextColumn(header, path, new DataGridLength(width));

        private void addTextColumn(string header, string path, DataGridLength width)
        {
            SyncGrid.Columns.Add(new DataGridTextColumn
            {
                Header = DataGridColumnFilterHelper.CreatePropertyFilterHeader(SyncGrid, header, path),
                Binding = new Binding(path) { Mode = BindingMode.OneWay },
                Width = width,
                IsReadOnly = true,
            });
        }

        private void configureSyncGridBehavior()
        {
            if (syncGridBehaviorConfigured || vm == null)
                return;

            syncGridBehaviorConfigured = true;

            CheckableDataGridHelper.Configure(
                SyncGrid,
                () => vm.SyncRows,
                (rows, check) => vm.Presenter.SetSyncRowsChecked(rows, check),
                () => vm.Presenter.InvertSyncRowChecks(),
                rows => vm.Presenter.DeleteSyncRowsAsync(rows),
                afterSelectionChanged: () => vm.OnSyncSelectionChanged());
        }

        private void refreshRealmCombos()
        {
            if (vm == null)
                return;

            EndpointACombo.ItemsSource = vm.RealmFiles;
            EndpointBCombo.ItemsSource = vm.RealmFiles;
            EndpointACombo.SelectedValue = vm.SyncRealmIdA;
            EndpointBCombo.SelectedValue = vm.SyncRealmIdB;
        }

        private void updateCategoryTabs()
        {
            if (vm == null)
                return;

            TabSourceOnly.Appearance = vm.CurrentCategory == DiffCategory.SourceOnly ? ControlAppearance.Primary : ControlAppearance.Secondary;
            TabTargetOnly.Appearance = vm.CurrentCategory == DiffCategory.TargetOnly ? ControlAppearance.Primary : ControlAppearance.Secondary;
            TabConflicted.Appearance = vm.CurrentCategory == DiffCategory.Conflicted ? ControlAppearance.Primary : ControlAppearance.Secondary;
        }

        private void updateButtons()
        {
            if (vm == null)
                return;

            ComputeButton.IsEnabled = ExecuteButton.IsEnabled = !vm.IsBusy;
        }

        private void EndpointACombo_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (vm != null && EndpointACombo.SelectedValue is string id)
                vm.SyncRealmIdA = id;
        }

        private void EndpointBCombo_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (vm != null && EndpointBCombo.SelectedValue is string id)
                vm.SyncRealmIdB = id;
        }

        private void SetOpCombo_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (vm != null && SetOpCombo.SelectedItem is ComboBoxItem { Tag: RealmSetOperation op })
                vm.SetOperation = op;
        }

        private void ActionCombo_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (vm != null && ActionCombo.SelectedItem is ComboBoxItem { Tag: RealmSyncAction action })
                vm.SyncAction = action;
        }

        private void WriteTargetCombo_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (vm != null && WriteTargetCombo.SelectedItem is ComboBoxItem { Tag: SyncWriteEndpoint endpoint })
                vm.SyncWriteTarget = endpoint;
        }

        private void EntityFilter_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (vm != null && EntityFilterCombo.SelectedItem is ComboBoxItem { Tag: EntityKindFilter filter })
                vm.EntityFilter = filter;
        }

        private void TabSourceOnly_OnClick(object sender, RoutedEventArgs e)
        {
            if (vm != null) vm.CurrentCategory = DiffCategory.SourceOnly;
        }

        private void TabTargetOnly_OnClick(object sender, RoutedEventArgs e)
        {
            if (vm != null) vm.CurrentCategory = DiffCategory.TargetOnly;
        }

        private void TabConflicted_OnClick(object sender, RoutedEventArgs e)
        {
            if (vm != null) vm.CurrentCategory = DiffCategory.Conflicted;
        }

        private void SelectAll_OnClick(object sender, RoutedEventArgs e) => vm?.ToggleSelectAllCommand.Execute(null);
        private void Compute_OnClick(object sender, RoutedEventArgs e) => vm?.ComputeSetCommand.Execute(null);
        private void Execute_OnClick(object sender, RoutedEventArgs e) => vm?.ExecuteSyncCommand.Execute(null);

        private void SyncGrid_OnCellChanged(object? sender, EventArgs e) => vm?.OnSyncSelectionChanged();
    }
}
