using System.Windows.Controls.Primitives;
using System.Windows.Data;
using osu.EzRealmSync.AppModel;
using osu.EzRealmSync.AppModel.Localization;
using osu.EzRealmSync.Desktop.ViewModels;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.Desktop.Pages
{
    public partial class ExportPage : UserControl
    {
        private ShellViewModel? vm;
        private bool suppressKindChange;

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
            setupDataKindCombo();

            RealmSelectCombo.ItemsSource = vm.RealmFiles;
            RealmSelectCombo.SelectedValue = vm.ExportRealmId ?? vm.SelectedRealmId;
            ExportDirBox.Text = vm.ExportDirectory;
            FolderNameBox.Text = vm.ExportFolderName;

            ExportGrid.ItemsSource = vm.ExportItems;

            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ShellViewModel.RealmFiles))
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
            bool enabled = vm?.CanUseFixAndExport == true && vm.IsBusy == false;
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
            RealmSelectCombo.SelectedValue = vm.ExportRealmId ?? vm.SelectedRealmId;
        }

        private void setupGrid()
        {
            if (ExportGrid.Columns.Count > 0)
                return;

            var checkFactory = new FrameworkElementFactory(typeof(CheckBox));
            checkFactory.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(RealmExportItemModel.IsSelected)) { Mode = BindingMode.TwoWay });
            checkFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            checkFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

            ExportGrid.Columns.Add(new DataGridTemplateColumn
            {
                Header = string.Empty,
                Width = 40,
                CellTemplate = new DataTemplate { VisualTree = checkFactory },
            });
            ExportGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColTitle"), Binding = new Binding(nameof(RealmExportItemModel.Title)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            ExportGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColArtist"), Binding = new Binding(nameof(RealmExportItemModel.Artist)), Width = 120 });
            ExportGrid.Columns.Add(new DataGridTextColumn
            {
                Header = Loc.Get("ColExportCollection"),
                Binding = new Binding(nameof(RealmExportItemModel.CollectionName)),
                Width = 120,
            });
            ExportGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColExportPath"), Binding = new Binding(nameof(RealmExportItemModel.RelativePath)), Width = new DataGridLength(1.5, DataGridLengthUnitType.Star) });
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
            if (vm == null || RealmSelectCombo.SelectedValue is not string id)
                return;

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
