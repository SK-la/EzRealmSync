using System.Windows.Data;
using osu.EzRealmSync.AppModel;
using osu.EzRealmSync.AppModel.Localization;
using osu.EzRealmSync.Desktop.Converters;
using osu.EzRealmSync.Desktop.Helpers;
using osu.EzRealmSync.Desktop.ViewModels;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.Desktop.Pages
{
    public partial class DataPage
    {
        private static readonly BrowseCellConverter browse_cell_converter = new BrowseCellConverter();

        private ShellViewModel? vm;
        private bool suppressClassSelection;
        private bool browseGridConfigured;

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
            configureBrowseGrid();
            refreshRealmCombo();
            refreshSummary();
            refreshClassList();
            refreshBrowseGrid();

            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ShellViewModel.RealmFiles)
                    || e.PropertyName == nameof(ShellViewModel.DataRealmId))
                    refreshRealmCombo();

                if (e.PropertyName is nameof(ShellViewModel.LoadedSnapshotSummary) or nameof(ShellViewModel.DataClasses))
                {
                    refreshSummary();
                    refreshClassList();
                }

                if (e.PropertyName is nameof(ShellViewModel.BrowseRows) or nameof(ShellViewModel.BrowseColumns))
                    refreshBrowseGrid();
            };

            vm.Presenter.SelectedRealmClass.BindValueChanged(_ => Dispatcher.Invoke(syncClassListSelection));
            vm.Presenter.BrowseTableChanged += () => Dispatcher.Invoke(refreshBrowseGrid);
        }

        private void configureBrowseGrid()
        {
            if (browseGridConfigured || vm == null)
                return;

            browseGridConfigured = true;

            DataGrid.ClipboardCopyMode = DataGridClipboardCopyMode.None;
            DataGrid.SelectionUnit = DataGridSelectionUnit.FullRow;
            DataGrid.SelectionMode = DataGridSelectionMode.Extended;

            DataGrid.SelectionChanged += (_, e) =>
            {
                CheckableDataGridHelper.SyncSelectionToChecks(
                    DataGrid,
                    e,
                    () => vm.BrowseRows,
                    (rows, check) => vm.Presenter.SetBrowseRowsChecked(rows, check));
            };

            DataGridContextMenuHelper.AttachExclusive(DataGrid, menu =>
            {
                DataGridContextMenuHelper.AddItem(menu, "check", Loc.Get("CtxCheck"), (_, _) =>
                    vm.Presenter.SetBrowseRowsChecked(CheckableDataGridHelper.GetContextTargets<RealmBrowseRowModel>(DataGrid), true));

                DataGridContextMenuHelper.AddItem(menu, "uncheck", Loc.Get("CtxUncheck"), (_, _) =>
                    vm.Presenter.SetBrowseRowsChecked(CheckableDataGridHelper.GetContextTargets<RealmBrowseRowModel>(DataGrid), false));

                DataGridContextMenuHelper.AddItem(menu, "invert", Loc.Get("CtxInvertCheck"), (_, _) => vm.Presenter.InvertBrowseRowChecks());

                var exportItem = DataGridContextMenuHelper.AddItem(menu, "export", Loc.Get("CtxExport"), async (_, _) =>
                {
                    var targets = CheckableDataGridHelper.GetContextTargets<RealmBrowseRowModel>(DataGrid);
                    if (targets.Count > 0)
                        await vm.Presenter.ExportBrowseRowsAsync(targets);
                });

                var deleteItem = DataGridContextMenuHelper.AddItem(menu, "delete", Loc.Get("CtxDelete"), async (_, _) =>
                {
                    var targets = CheckableDataGridHelper.GetContextTargets<RealmBrowseRowModel>(DataGrid);
                    if (targets.Count > 0)
                        await vm.Presenter.DeleteBrowseRowsAsync(targets);
                });

                menu.Opened += (_, _) =>
                {
                    var cls = vm.SelectedRealmClass;
                    exportItem.IsEnabled = RealmAppPresenter.IsExportableBrowseClass(cls);
                    deleteItem.IsEnabled = RealmAppPresenter.IsMutableBrowseClass(cls);
                };
            });
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
            RealmSelectCombo.SelectedValue = vm.DataRealmId;
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
            DataGrid.ItemsSource = vm.BrowseRows;
        }

        private void rebuildBrowseColumns(IReadOnlyList<RealmColumnDefinition> columns)
        {
            DataGrid.Columns.Clear();

            var checkFactory = new FrameworkElementFactory(typeof(CheckBox));
            checkFactory.SetBinding(
                CheckBox.IsCheckedProperty,
                new Binding(nameof(RealmBrowseRowModel.IsSelected))
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                });
            checkFactory.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            checkFactory.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);

            DataGrid.Columns.Add(new DataGridTemplateColumn
            {
                Header = string.Empty,
                Width = 40,
                CellTemplate = new DataTemplate { VisualTree = checkFactory },
            });

            foreach (var column in columns)
            {
                DataGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = formatColumnHeader(column),
                    Binding = new Binding
                    {
                        Converter = browse_cell_converter,
                        ConverterParameter = column.PropertyKey,
                    },
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
            if (vm != null && RealmSelectCombo.SelectedValue is string id)
                vm.DataRealmId = id;
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
