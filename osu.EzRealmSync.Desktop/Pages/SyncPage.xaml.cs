using System.Windows.Data;
using osu.EzRealmSync.AppModel;
using osu.EzRealmSync.AppModel.Localization;
using osu.EzRealmSync.Desktop.ViewModels;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.Desktop.Pages
{
    public partial class SyncPage : UserControl
    {
        private ShellViewModel? vm;

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
            refreshRealmCombos();
            SyncGrid.ItemsSource = vm.SyncRows;

            vm.PropertyChanged += (_, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    switch (e.PropertyName)
                    {
                        case nameof(ShellViewModel.RealmFiles):
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

            vm.Presenter.SyncRowsChanged += () => Dispatcher.Invoke(() => SyncGrid.ItemsSource = vm.SyncRows);
            vm.Presenter.LabelsChanged += () => Dispatcher.Invoke(refreshTabLabels);

            updateCategoryTabs();
            updateButtons();
        }

        private void refreshLabels()
        {
            SourceLabel.Text = Loc.Get("SourceRealm");
            TargetLabel.Text = Loc.Get("TargetRealm");
            SetOpLabel.Text = Loc.Get("SetOperation");
            ActionLabel.Text = Loc.Get("SyncAction");
            ComputeButton.Content = Loc.Get("ComputeSet");
            ExecuteButton.Content = Loc.Get("ExecuteAction");
            refreshTabLabels();
        }

        private void refreshTabLabels()
        {
            if (vm == null)
                return;

            TabSourceOnly.Content = Loc.Get("TabOnlyInA");
            TabTargetOnly.Content = Loc.Get("TabOnlyInB");
            TabConflicted.Content = Loc.Get("TabConflicted");
            SelectAllButton.Content = Loc.Get("SelectAll");
        }

        private void setupCombos()
        {
            if (SetOpCombo.Items.Count > 0)
                return;

            foreach (var op in vm!.SetOperations)
                SetOpCombo.Items.Add(new ComboBoxItem { Content = vm.GetSetOperationLabel(op), Tag = op });

            foreach (var action in vm.SyncActions)
                ActionCombo.Items.Add(new ComboBoxItem { Content = vm.GetSyncActionLabel(action), Tag = action });

            EntityFilterCombo.Items.Clear();
            foreach (var filter in vm.EntityFilters)
                EntityFilterCombo.Items.Add(new ComboBoxItem { Content = vm.GetEntityFilterLabel(filter), Tag = filter });

            SetOpCombo.SelectedIndex = 2;
            ActionCombo.SelectedIndex = 0;
            EntityFilterCombo.SelectedIndex = 0;
        }

        private void setupSyncGrid()
        {
            if (SyncGrid.Columns.Count > 0)
                return;

            var checkColumn = new DataGridCheckBoxColumn
            {
                Binding = new Binding(nameof(DiffRowModel.IsSelected)) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                Width = 40,
            };

            if (SyncGrid.CheckBoxColumnElementStyle != null)
            {
                checkColumn.ElementStyle = SyncGrid.CheckBoxColumnElementStyle;
                checkColumn.EditingElementStyle = SyncGrid.CheckBoxColumnEditingElementStyle;
            }

            SyncGrid.Columns.Add(checkColumn);
            SyncGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColKind"), Binding = new Binding(nameof(DiffRowModel.EntityKind)), Width = 80 });
            SyncGrid.Columns.Add(new DataGridTextColumn
                { Header = Loc.Get("ColTitle"), Binding = new Binding(nameof(DiffRowModel.Title)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            SyncGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColArtist"), Binding = new Binding(nameof(DiffRowModel.Artist)), Width = 110 });
            SyncGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColHash"), Binding = new Binding(nameof(DiffRowModel.Hash)), Width = 100 });
            SyncGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColRuleset"), Binding = new Binding(nameof(DiffRowModel.Ruleset)), Width = 72 });
            SyncGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColDate"), Binding = new Binding(nameof(DiffRowModel.Date)), Width = 120 });
        }

        private void refreshRealmCombos()
        {
            if (vm == null)
                return;

            SourceCombo.ItemsSource = vm.RealmFiles;
            TargetCombo.ItemsSource = vm.RealmFiles;
            SourceCombo.SelectedValue = vm.ActiveSourceRealmId;
            TargetCombo.SelectedValue = vm.ActiveTargetRealmId;
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

        private void SourceCombo_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (vm != null && SourceCombo.SelectedValue is string id)
                vm.ActiveSourceRealmId = id;
        }

        private void TargetCombo_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (vm != null && TargetCombo.SelectedValue is string id)
                vm.ActiveTargetRealmId = id;
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
