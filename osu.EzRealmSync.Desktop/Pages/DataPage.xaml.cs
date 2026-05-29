using System.Data;
using System.Windows.Data;
using osu.EzRealmSync.AppModel;
using osu.EzRealmSync.AppModel.Localization;
using osu.EzRealmSync.Desktop.Helpers;
using osu.EzRealmSync.Desktop.ViewModels;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.Desktop.Pages
{
    public partial class DataPage : UserControl
    {
        private ShellViewModel? vm;
        private bool suppressClassSelection;

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
            ClassesHeader.Text = Loc.Get("DataClasses");
            attachBrowseContextMenu();
            refreshRealmCombo();
            refreshSummary();
            refreshClassList();
            refreshBrowseGrid();

            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ShellViewModel.RealmFiles))
                    refreshRealmCombo();

                if (e.PropertyName is nameof(ShellViewModel.LoadedSnapshotSummary) or nameof(ShellViewModel.DataClasses))
                {
                    refreshSummary();
                    refreshClassList();
                }

                if (e.PropertyName is nameof(ShellViewModel.BrowseDataView) or nameof(ShellViewModel.BrowseColumns))
                    refreshBrowseGrid();
            };

            vm.Presenter.SelectedRealmClass.BindValueChanged(_ => Dispatcher.Invoke(syncClassListSelection));
        }

        private void attachBrowseContextMenu()
        {
            DataGridContextMenuHelper.Attach(DataGrid, menu =>
            {
                DataGridContextMenuHelper.AddItem(menu, "delete", Loc.Get("CtxDelete"), (_, _) => deleteSelectedBrowseRows());
            });
        }

        private void deleteSelectedBrowseRows()
        {
            if (vm == null)
                return;

            var ids = collectSelectedBrowseRowIds();
            if (ids.Count == 0)
                return;

            vm.Presenter.DeleteBrowseRows(ids);
            refreshSummary();
            refreshClassList();
            refreshBrowseGrid();
        }

        private List<Guid> collectSelectedBrowseRowIds()
        {
            var ids = new List<Guid>();

            foreach (var item in DataGrid.SelectedItems)
            {
                if (item is DataRowView rowView)
                    tryAddRowId(rowView, ids);
            }

            if (ids.Count == 0 && DataGrid.CurrentItem is DataRowView current)
                tryAddRowId(current, ids);

            return ids;
        }

        private static void tryAddRowId(DataRowView rowView, List<Guid> ids)
        {
            if (!rowView.Row.Table.Columns.Contains(RealmAppPresenter.BrowseIdColumn))
                return;

            var value = rowView[RealmAppPresenter.BrowseIdColumn];
            if (value is Guid id)
                ids.Add(id);
        }

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

        private void refreshClassList()
        {
            if (vm == null)
                return;

            ClassList.ItemsSource = vm.DataClasses;
            syncClassListSelection();
        }

        private void syncClassListSelection()
        {
            if (vm == null || ClassList.Items.Count == 0)
                return;

            suppressClassSelection = true;
            foreach (RealmClassListItemModel item in ClassList.Items)
            {
                if (item.Class == vm.SelectedRealmClass)
                {
                    ClassList.SelectedItem = item;
                    break;
                }
            }

            suppressClassSelection = false;
        }

        private void refreshBrowseGrid()
        {
            if (vm == null)
                return;

            rebuildBrowseColumns(vm.BrowseColumns);
            DataGrid.ItemsSource = vm.BrowseDataView;
        }

        private void rebuildBrowseColumns(IReadOnlyList<RealmColumnDefinition> columns)
        {
            DataGrid.Columns.Clear();

            foreach (var column in columns)
            {
                DataGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = formatColumnHeader(column),
                    Binding = new Binding($"[{column.PropertyKey}]") { Mode = BindingMode.OneWay },
                    Width = DataGridLength.Auto,
                    MinWidth = 72,
                    IsReadOnly = true,
                });
            }
        }

        private static string formatColumnHeader(RealmColumnDefinition column)
        {
            if (string.IsNullOrWhiteSpace(column.TypeHint))
                return column.Header;

            return $"{column.Header}\n{column.TypeHint}";
        }

        private void RealmSelectCombo_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (vm == null || RealmSelectCombo.SelectedValue is not string id)
                return;

            vm.SelectedRealmId = id;
        }

        private void ClassList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressClassSelection || vm == null || ClassList.SelectedItem is not RealmClassListItemModel item)
                return;

            vm.SelectedRealmClass = item.Class;
        }

        private void Load_OnClick(object sender, RoutedEventArgs e) => vm?.LoadRealmCommand.Execute(null);
    }
}
