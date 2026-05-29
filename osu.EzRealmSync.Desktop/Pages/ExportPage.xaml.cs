using System.Windows.Data;
using osu.EzRealmSync.AppModel;
using osu.EzRealmSync.AppModel.Localization;
using osu.EzRealmSync.Desktop.Helpers;
using osu.EzRealmSync.Desktop.ViewModels;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.Desktop.Pages
{
    public partial class ExportPage
    {
        private ShellViewModel? vm;
        private bool suppressKindChange;
        private bool exportGridBehaviorConfigured;

        public ExportPage()
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
            setupGrid();
            configureExportGridBehavior();
            setupDataKindCombo();

            refreshRealmCombo();
            ExportDirBox.Text = vm.ExportDirectory;
            FolderNameBox.Text = vm.ExportFolderName;

            ExportGrid.ItemsSource = vm.ExportItems;

            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ShellViewModel.RealmFiles)
                    || e.PropertyName == nameof(ShellViewModel.ExportRealmId))
                    Dispatcher.Invoke(refreshRealmCombo);

                if (e.PropertyName == nameof(ShellViewModel.ExportItems))
                    Dispatcher.Invoke(() => ExportGrid.ItemsSource = vm!.ExportItems);

                if (e.PropertyName is nameof(ShellViewModel.ExportDirectory) or nameof(ShellViewModel.ExportFolderName))
                {
                    Dispatcher.Invoke(() =>
                    {
                        ExportDirBox.Text = vm!.ExportDirectory;
                        FolderNameBox.Text = vm.ExportFolderName;
                    });
                }

                if (e.PropertyName == nameof(ShellViewModel.CanUseFixAndExport))
                    Dispatcher.Invoke(updateEnabled);
            };

            vm.Presenter.ExportItemsChanged += () => Dispatcher.Invoke(() => ExportGrid.ItemsSource = vm!.ExportItems);
            vm.Presenter.WorkspaceCapabilitiesChanged += () => Dispatcher.Invoke(updateEnabled);

            updateEnabled();
            updateExportGridColumns();
        }

        private void updateEnabled()
        {
            bool enabled = vm is { CanUseFixAndExport: true, IsBusy: false };
            LoadListButton.IsEnabled = enabled;
            ExportButton.IsEnabled = enabled;
            SelectAllButton.IsEnabled = enabled;
            RealmSelectCombo.IsEnabled = enabled;
            DataKindCombo.IsEnabled = enabled;
            ExportDirBox.IsEnabled = enabled;
            BrowseExportButton.IsEnabled = enabled;
            FolderNameBox.IsEnabled = enabled;
        }

        private void refreshLabels()
        {
            HintText.Text = Loc.Get("FixRequiresFilesHint");
            ExportDirLabel.Text = Loc.Get("ExportDirectory");
            LoadListButton.Content = Loc.Get("ExportLoadList");
            ExportButton.Content = Loc.Get("ExportRun");
            BrowseExportButton.Content = Loc.Get("Browse");
            SelectAllButton.Content = Loc.Get("SelectAll");
            FolderNameBox.ToolTip = Loc.Get("ExportFolderNameHint");
        }

        private void setupDataKindCombo()
        {
            suppressKindChange = true;
            DataKindCombo.Items.Clear();

            foreach (ExportDataKind kind in Enum.GetValues<ExportDataKind>())
            {
                DataKindCombo.Items.Add(new ComboBoxItem
                {
                    Content = vm!.GetExportDataKindLabel(kind),
                    Tag = kind,
                });
            }

            DataKindCombo.SelectedItem = DataKindCombo.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (ExportDataKind)i.Tag! == vm!.ExportDataKind);
            suppressKindChange = false;
        }

        private void refreshRealmCombo()
        {
            if (vm == null)
                return;

            RealmSelectCombo.ItemsSource = vm.RealmFiles;
            RealmSelectCombo.SelectedValue = vm.ExportRealmId;
        }

        private void setupGrid()
        {
            if (ExportGrid.Columns.Count > 0)
                return;

            var checkFactory = new FrameworkElementFactory(typeof(CheckBox));
            checkFactory.SetBinding(
                CheckBox.IsCheckedProperty,
                new Binding(nameof(RealmExportItemModel.IsSelected))
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                });
            checkFactory.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            checkFactory.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);

            ExportGrid.Columns.Add(new DataGridTemplateColumn
            {
                Header = string.Empty,
                Width = 40,
                CellTemplate = new DataTemplate { VisualTree = checkFactory },
            });
            addTextColumn(Loc.Get("ColTitle"), nameof(RealmExportItemModel.Title), new DataGridLength(1, DataGridLengthUnitType.Star));
            addTextColumn(Loc.Get("ColArtist"), nameof(RealmExportItemModel.Artist), 120);
            addTextColumn(Loc.Get("ColExportCollection"), nameof(RealmExportItemModel.CollectionName), 120);
            addTextColumn(Loc.Get("ColExportPath"), nameof(RealmExportItemModel.RelativePath), new DataGridLength(1.5, DataGridLengthUnitType.Star));
        }

        private void addTextColumn(string header, string path, double width) => addTextColumn(header, path, new DataGridLength(width));

        private void addTextColumn(string header, string path, DataGridLength width)
        {
            ExportGrid.Columns.Add(new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(path) { Mode = BindingMode.OneWay },
                Width = width,
                IsReadOnly = true,
            });
        }

        private void configureExportGridBehavior()
        {
            if (exportGridBehaviorConfigured || vm == null)
                return;

            exportGridBehaviorConfigured = true;

            CheckableDataGridHelper.Configure(
                ExportGrid,
                () => vm.ExportItems,
                (rows, check) => vm.Presenter.SetExportItemsChecked(rows, check),
                () => vm.Presenter.InvertExportItemChecks(),
                rows => vm.Presenter.DeleteExportItemsAsync(rows));
        }

        private void updateExportGridColumns()
        {
            if (ExportGrid.Columns.Count < 4 || vm == null)
                return;

            bool showCollection = vm.ExportDataKind == ExportDataKind.Collection;
            ExportGrid.Columns[3].Visibility = showCollection ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RealmSelectCombo_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (vm != null && RealmSelectCombo.SelectedValue is string id)
                vm.ExportRealmId = id;
        }

        private void DataKindCombo_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressKindChange || vm == null || DataKindCombo.SelectedItem is not ComboBoxItem { Tag: ExportDataKind kind })
                return;

            vm.ExportDataKind = kind;
            updateExportGridColumns();
        }

        private void LoadList_OnClick(object sender, RoutedEventArgs e) => vm?.LoadExportCatalogCommand.Execute(null);

        private void Export_OnClick(object sender, RoutedEventArgs e)
        {
            if (vm == null)
                return;

            vm.ExportDirectory = ExportDirBox.Text;
            vm.ExportFolderName = FolderNameBox.Text;
            vm.ExportSelectedCommand.Execute(null);
        }

        private void BrowseExport_OnClick(object sender, RoutedEventArgs e) => vm?.BrowseExportDirectoryCommand.Execute(null);

        private void SelectAll_OnClick(object sender, RoutedEventArgs e) => vm?.ToggleExportSelectAllCommand.Execute(null);
    }
}
