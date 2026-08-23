using System.Windows.Controls.Primitives;
using System.Windows.Data;
using osu.EzRealmSync.AppModel;
using osu.EzRealmSync.AppModel.Localization;
using osu.EzRealmSync.Desktop.Helpers;
using osu.EzRealmSync.Desktop.ViewModels;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.Desktop.Pages
{
    public partial class ImportPage
    {
        private ShellViewModel? vm;
        private bool pageBound;
        private bool realmGridConfigured;
        private bool realmGridSetup;

        public ImportPage()
        {
            InitializeComponent();
            Loaded += (_, _) => bindIfReady();
            DataContextChanged += (_, _) => bindIfReady();
        }

        private void bindIfReady()
        {
            if (pageBound || DataContext is not ShellViewModel shell)
                return;

            pageBound = true;
            vm = shell;
            refreshLabels();
            setupRealmGrid();
            configureRealmGrid();

            SearchDirBox.Text = vm.SearchDirectory;
            BackupDirBox.Text = vm.BackupDirectory;

            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ShellViewModel.SearchDirectory))
                    Dispatcher.Invoke(() => SearchDirBox.Text = vm.SearchDirectory);

                if (e.PropertyName == nameof(ShellViewModel.BackupDirectory))
                    Dispatcher.Invoke(() => BackupDirBox.Text = vm.BackupDirectory);

                if (e.PropertyName == nameof(ShellViewModel.RealmFileRows))
                    Dispatcher.Invoke(() => RealmFilesGrid.ItemsSource = vm!.RealmFileRows);
            };

            vm.Presenter.RealmFilesChanged += () => Dispatcher.Invoke(() => RealmFilesGrid.ItemsSource = vm!.RealmFileRows);

            SearchDirBox.LostFocus += (_, _) => applySearchFromBox();
            BackupDirBox.LostFocus += (_, _) => applyBackupFromBox();

            BackupCombo.ItemsSource = vm.Presenter.BackupEntries;
            BackupCombo.SetBinding(
                Selector.SelectedValueProperty,
                new Binding(nameof(ShellViewModel.SelectedBackupId))
                {
                    Source = vm,
                    Mode = BindingMode.TwoWay,
                });

            vm.Presenter.BackupEntriesChanged += () => Dispatcher.Invoke(() => BackupCombo.ItemsSource = vm!.Presenter.BackupEntries);
        }

        private void configureRealmGrid()
        {
            if (realmGridConfigured || vm == null)
                return;

            realmGridConfigured = true;
            RealmFilesGrid.ItemsSource = vm.RealmFileRows;

            CheckableDataGridHelper.Configure(
                RealmFilesGrid,
                () => vm.RealmFileRows,
                (rows, check) => vm.Presenter.SetRealmFileRowsChecked(rows, check),
                () => vm.Presenter.InvertRealmFileRowChecks(),
                rows => vm.Presenter.DeleteRealmFileRowsAsync(rows));
        }

        private void applySearchFromBox()
        {
            if (vm == null)
                return;

            vm.SearchDirectory = SearchDirBox.Text;
        }

        private void applyBackupFromBox()
        {
            if (vm == null)
                return;

            vm.BackupDirectory = BackupDirBox.Text;
        }

        private void refreshLabels()
        {
            DropHintText.Text = Loc.Get("ImportDropHint");
            SearchDirLabel.Text = Loc.Get("SearchDirectory");
            BrowseSearchButton.Content = Loc.Get("Browse");
            ApplySearchButton.Content = Loc.Get("ApplySearchDirectory");
            RefreshButton.Content = Loc.Get("RefreshList");
            BackupDirLabel.Text = Loc.Get("BackupDirectory");
            BrowseBackupButton.Content = Loc.Get("Browse");
            BackupButton.Content = Loc.Get("BackupSelected");
            BackupListLabel.Text = Loc.Get("BackupList");
            RefreshBackupsButton.Content = Loc.Get("RefreshBackups");
            RestoreBackupButton.Content = Loc.Get("RestoreBackup");
            RealmListTitle.Text = Loc.Get("RealmFileList");
        }

        private void setupRealmGrid()
        {
            if (realmGridSetup)
                return;

            realmGridSetup = true;

            RealmFilesGrid.Columns.Add(DataGridCheckColumnHelper.CreateColumn());

            RealmFilesGrid.Columns.Add(new DataGridTextColumn
            {
                Header = DataGridColumnFilterHelper.CreatePropertyFilterHeader(RealmFilesGrid, Loc.Get("ColRealmName"), nameof(RealmFileRowModel.DisplayName)),
                Binding = new Binding(nameof(RealmFileRowModel.DisplayName)) { Mode = BindingMode.OneWay },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                IsReadOnly = true,
            });
            RealmFilesGrid.Columns.Add(new DataGridTextColumn
            {
                Header = DataGridColumnFilterHelper.CreatePropertyFilterHeader(RealmFilesGrid, Loc.Get("ColOfficialSchema"), nameof(RealmFileRowModel.OfficialSchemaDisplay)),
                Binding = new Binding(nameof(RealmFileRowModel.OfficialSchemaDisplay)) { Mode = BindingMode.OneWay },
                Width = 64,
                IsReadOnly = true,
            });
            RealmFilesGrid.Columns.Add(new DataGridTextColumn
            {
                Header = DataGridColumnFilterHelper.CreatePropertyFilterHeader(RealmFilesGrid, Loc.Get("ColEzSchema"), nameof(RealmFileRowModel.EzSchemaDisplay)),
                Binding = new Binding(nameof(RealmFileRowModel.EzSchemaDisplay)) { Mode = BindingMode.OneWay },
                Width = 56,
                IsReadOnly = true,
            });
            RealmFilesGrid.Columns.Add(new DataGridTextColumn
            {
                Header = DataGridColumnFilterHelper.CreatePropertyFilterHeader(RealmFilesGrid, Loc.Get("ColSize"), nameof(RealmFileRowModel.SizeDisplay)),
                Binding = new Binding(nameof(RealmFileRowModel.SizeDisplay)) { Mode = BindingMode.OneWay },
                Width = 80,
                IsReadOnly = true,
            });
            RealmFilesGrid.Columns.Add(new DataGridTextColumn
            {
                Header = DataGridColumnFilterHelper.CreatePropertyFilterHeader(RealmFilesGrid, Loc.Get("ColRealmPath"), nameof(RealmFileRowModel.FilePath)),
                Binding = new Binding(nameof(RealmFileRowModel.FilePath)) { Mode = BindingMode.OneWay },
                Width = new DataGridLength(2, DataGridLengthUnitType.Star),
                IsReadOnly = true,
            });
        }

        private void DropZone_OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void DropZone_OnDrop(object sender, DragEventArgs e)
        {
            if (vm == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
                return;

            SafeAsyncInvoker.Run(() => handleDropAsync(files), reportDropError);
        }

        private async Task handleDropAsync(string[] files)
        {
            foreach (var action in ImportDropProcessor.ParseDroppedPaths(files))
            {
                switch (action.Kind)
                {
                    case ImportDropActionKind.RegisterRealm:
                        vm!.SearchDirectory = RealmWorkspaceDiscovery.NormalizeStorageRoot(action.Path);
                        SearchDirBox.Text = vm.SearchDirectory;
                        await vm.RegisterDroppedRealmAsync(action.Path).ConfigureAwait(true);
                        break;

                    case ImportDropActionKind.SetSearchDirectory:
                        vm!.SearchDirectory = RealmWorkspaceDiscovery.NormalizeStorageRoot(action.Path);
                        SearchDirBox.Text = vm.SearchDirectory;
                        vm.RefreshRealmFilesCommand.Execute(null);
                        break;
                }
            }
        }

        private void reportDropError(Exception ex) => vm?.Presenter.MarshalToUi?.Invoke(() => vm.Presenter.StatusMessage.Value = ex.Message);

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            DropZone.DragOver += DropZone_OnDragOver;
            DropZone.Drop += DropZone_OnDrop;
            DropZone.MouseLeftButtonUp += DropZone_OnClick;
            DropZone.MouseEnter += (_, _) => DropZone.Opacity = 0.92;
            DropZone.MouseLeave += (_, _) => DropZone.Opacity = 1;
        }

        private void DropZone_OnClick(object sender, MouseButtonEventArgs e) => BrowseSearch_OnClick(sender, e);

        private void ApplySearch_OnClick(object sender, RoutedEventArgs e)
        {
            if (vm == null)
                return;

            applySearchFromBox();
            vm.ApplySearchDirectoryCommand.Execute(null);
        }

        private void Refresh_OnClick(object sender, RoutedEventArgs e)
        {
            if (vm == null)
                return;

            applySearchFromBox();
            vm.RefreshRealmFilesCommand.Execute(null);
        }

        private void BrowseSearch_OnClick(object sender, RoutedEventArgs e) => vm?.BrowseSearchDirectoryCommand.Execute(null);

        private void BrowseBackup_OnClick(object sender, RoutedEventArgs e) => vm?.BrowseBackupCommand.Execute(null);

        private void Backup_OnClick(object sender, RoutedEventArgs e) => vm?.BackupSelectedCommand.Execute(null);

        private void RefreshBackups_OnClick(object sender, RoutedEventArgs e) => vm?.RefreshBackupsCommand.Execute(null);

        private void RestoreBackup_OnClick(object sender, RoutedEventArgs e) => vm?.RestoreBackupCommand.Execute(null);

        private void RealmFilesGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (vm == null || RealmFilesGrid.SelectedItem is not RealmFileRowModel row)
                return;

            vm.ImportSelectedRealmId = row.Id;
        }
    }
}
