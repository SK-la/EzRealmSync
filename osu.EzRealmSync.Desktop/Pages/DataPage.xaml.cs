using System.Windows.Data;
using osu.EzRealmSync.AppModel;
using osu.EzRealmSync.AppModel.Localization;
using osu.EzRealmSync.Desktop.ViewModels;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.Desktop.Pages
{
    public partial class DataPage : UserControl
    {
        private ShellViewModel? vm;

        public DataPage()
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
            LoadButton.Content = Loc.Get("LoadRealm");
            setupDataGrid();
            setupGroupTabs();
            refreshRealmCombo();
            refreshSummary();
            updateGroupTabAppearance();

            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ShellViewModel.RealmFiles))
                    refreshRealmCombo();

                if (e.PropertyName is nameof(ShellViewModel.LoadedSnapshotSummary) or nameof(ShellViewModel.DataRows))
                {
                    refreshSummary();
                    DataGrid.ItemsSource = vm!.DataRows;
                }

                if (e.PropertyName == nameof(ShellViewModel.SelectedDataGroup))
                    updateGroupTabAppearance();
            };

            vm.Presenter.SelectedDataGroup.BindValueChanged(_ => Dispatcher.Invoke(updateGroupTabAppearance));

            DataGrid.ItemsSource = vm.DataRows;
        }

        private void setupGroupTabs()
        {
            GroupTabBeatmapSet.Content = vm!.GetEntityKindLabel(EntityKind.BeatmapSet);
            GroupTabBeatmap.Content = vm.GetEntityKindLabel(EntityKind.Beatmap);
            GroupTabScore.Content = vm.GetEntityKindLabel(EntityKind.Score);
        }

        private void updateGroupTabAppearance()
        {
            if (vm == null)
                return;

            setGroupAppearance(GroupTabBeatmapSet, vm.SelectedDataGroup == EntityKind.BeatmapSet);
            setGroupAppearance(GroupTabBeatmap, vm.SelectedDataGroup == EntityKind.Beatmap);
            setGroupAppearance(GroupTabScore, vm.SelectedDataGroup == EntityKind.Score);
        }

        private static void setGroupAppearance(Button button, bool active) => button.Appearance = active ? ControlAppearance.Primary : ControlAppearance.Secondary;

        private void refreshSummary()
        {
            SummaryText.Text = string.IsNullOrEmpty(vm?.LoadedSnapshotSummary)
                ? Loc.Get("SelectRealmHint")
                : vm!.LoadedSnapshotSummary;
        }

        private void refreshRealmCombo()
        {
            if (vm == null)
                return;

            RealmSelectCombo.ItemsSource = vm.RealmFiles;
            RealmSelectCombo.SelectedValue = vm.SelectedRealmId;
        }

        private void setupDataGrid()
        {
            if (DataGrid.Columns.Count > 0)
                return;

            DataGrid.Columns.Add(new DataGridTextColumn
                { Header = Loc.Get("ColTitle"), Binding = new Binding(nameof(RealmEntityRowModel.Title)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            DataGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColArtist"), Binding = new Binding(nameof(RealmEntityRowModel.Artist)), Width = 120 });
            DataGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColHash"), Binding = new Binding(nameof(RealmEntityRowModel.Hash)), Width = 100 });
            DataGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColRuleset"), Binding = new Binding(nameof(RealmEntityRowModel.Ruleset)), Width = 72 });
            DataGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColDate"), Binding = new Binding(nameof(RealmEntityRowModel.Date)), Width = 130 });
            DataGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColExtra"), Binding = new Binding(nameof(RealmEntityRowModel.Extra)), Width = 100 });
        }

        private void RealmSelectCombo_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (vm == null || RealmSelectCombo.SelectedValue is not string id)
                return;

            vm.SelectedRealmId = id;
        }

        private void GroupTab_OnClick(object sender, RoutedEventArgs e)
        {
            if (vm == null || sender is not Button { Tag: string tag })
                return;

            if (Enum.TryParse<EntityKind>(tag, out var kind))
                vm.SelectedDataGroup = kind;
        }

        private void Load_OnClick(object sender, RoutedEventArgs e) => vm?.LoadRealmCommand.Execute(null);
    }
}
