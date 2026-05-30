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
        private DataGridTextColumn? colSecondary;
        private DataGridTextColumn? colExtra;
        private DataGridTextColumn? colPath;

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
            GroupScoresByPlayerCheck.IsChecked = vm.ExportGroupScoresByPlayer;

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

                if (e.PropertyName == nameof(ShellViewModel.ExportGroupScoresByPlayer))
                    Dispatcher.Invoke(() => GroupScoresByPlayerCheck.IsChecked = vm!.ExportGroupScoresByPlayer);

                if (e.PropertyName == nameof(ShellViewModel.CanUseFixAndExport))
                    Dispatcher.Invoke(updateEnabled);

                if (e.PropertyName == nameof(ShellViewModel.ExportDataKind))
                    Dispatcher.Invoke(updateExportGridColumns);
            };

            vm.Presenter.ExportItemsChanged += () => Dispatcher.Invoke(() => ExportGrid.ItemsSource = vm!.ExportItems);
            vm.Presenter.WorkspaceCapabilitiesChanged += () => Dispatcher.Invoke(updateEnabled);

            GroupScoresByPlayerCheck.Checked += (_, _) => setGroupScoresByPlayer(true);
            GroupScoresByPlayerCheck.Unchecked += (_, _) => setGroupScoresByPlayer(false);

            updateEnabled();
            updateExportGridColumns();
        }

        private void setGroupScoresByPlayer(bool value)
        {
            if (vm != null)
                vm.ExportGroupScoresByPlayer = value;
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
            GroupScoresByPlayerCheck.IsEnabled = enabled;
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
            GroupScoresByPlayerCheck.Content = Loc.Get("ExportGroupScoresByPlayer");
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

            ExportGrid.Columns.Add(DataGridCheckColumnHelper.CreateColumn());

            colSecondary = addTextColumn(Loc.Get("ColArtist"), nameof(RealmExportItemModel.Artist), 120);
            colExtra = addTextColumn(Loc.Get("ColExportPlayer"), nameof(RealmExportItemModel.PlayerName), 120);
            colPath = addTextColumn(Loc.Get("ColExportPath"), nameof(RealmExportItemModel.RelativePath), new DataGridLength(1.5, DataGridLengthUnitType.Star));

            ExportGrid.Columns.Insert(1, addTextColumn(Loc.Get("ColTitle"), nameof(RealmExportItemModel.Title), new DataGridLength(1, DataGridLengthUnitType.Star)));
        }

        private DataGridTextColumn addTextColumn(string header, string path, double width) => addTextColumn(header, path, new DataGridLength(width));

        private DataGridTextColumn addTextColumn(string header, string path, DataGridLength width)
        {
            var column = new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(path) { Mode = BindingMode.OneWay },
                Width = width,
                IsReadOnly = true,
            };

            ExportGrid.Columns.Add(column);
            return column;
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
            if (colSecondary == null || colExtra == null || colPath == null || vm == null)
                return;

            bool isCollection = vm.ExportDataKind == ExportDataKind.Collection;
            bool isScore = vm.ExportDataKind == ExportDataKind.Score;

            GroupScoresByPlayerCheck.Visibility = isScore ? Visibility.Visible : Visibility.Collapsed;

            colSecondary.Header = isCollection ? Loc.Get("ColBeatmapCount") : Loc.Get("ColArtist");
            colSecondary.Binding = new Binding(isCollection ? nameof(RealmExportItemModel.BeatmapCountLabel) : nameof(RealmExportItemModel.Artist))
            {
                Mode = BindingMode.OneWay,
            };

            colExtra.Visibility = isScore ? Visibility.Visible : Visibility.Collapsed;
            colExtra.Header = Loc.Get("ColExportPlayer");

            colPath.Visibility = isCollection ? Visibility.Collapsed : Visibility.Visible;
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
            vm.Presenter.ClearExportItems();
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
