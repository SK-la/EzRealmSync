using System.Windows.Data;
using osu.EzRealmSync.AppModel;
using osu.EzRealmSync.AppModel.Localization;
using osu.EzRealmSync.Desktop.Helpers;
using osu.EzRealmSync.Desktop.ViewModels;

namespace osu.EzRealmSync.Desktop.Pages
{
    public partial class ImportPage
    {
        private ShellViewModel? vm;
        private bool realmGridConfigured;

        public ImportPage()
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
            setupRealmGrid();
            configureRealmGrid();

            EndpointABox.Text = vm.EndpointAWorkspace;
            EndpointBBox.Text = vm.EndpointBWorkspace;
            BackupDirBox.Text = vm.BackupDirectory;

            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ShellViewModel.EndpointAWorkspace))
                    Dispatcher.Invoke(() => EndpointABox.Text = vm.EndpointAWorkspace);

                if (e.PropertyName == nameof(ShellViewModel.EndpointBWorkspace))
                    Dispatcher.Invoke(() => EndpointBBox.Text = vm.EndpointBWorkspace);

                if (e.PropertyName == nameof(ShellViewModel.BackupDirectory))
                    Dispatcher.Invoke(() => BackupDirBox.Text = vm.BackupDirectory);

                if (e.PropertyName == nameof(ShellViewModel.RealmFileRows))
                    Dispatcher.Invoke(() => RealmFilesGrid.ItemsSource = vm!.RealmFileRows);
            };

            vm.Presenter.RealmFilesChanged += () => Dispatcher.Invoke(() => RealmFilesGrid.ItemsSource = vm!.RealmFileRows);

            EndpointABox.LostFocus += (_, _) => applyEndpointPathsFromBoxes();
            EndpointBBox.LostFocus += (_, _) => applyEndpointPathsFromBoxes();
            BackupDirBox.LostFocus += (_, _) => applyBackupFromBox();

            BackupCombo.ItemsSource = vm.Presenter.BackupEntries;
            BackupCombo.SetBinding(
                System.Windows.Controls.Primitives.Selector.SelectedValueProperty,
                new System.Windows.Data.Binding(nameof(ShellViewModel.SelectedBackupId))
                {
                    Source = vm,
                    Mode = System.Windows.Data.BindingMode.TwoWay,
                });

            vm.Presenter.BackupEntriesChanged += () =>
                Dispatcher.Invoke(() => BackupCombo.ItemsSource = vm!.Presenter.BackupEntries);
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

        private void applyEndpointPathsFromBoxes()
        {
            if (vm == null)
                return;

            vm.EndpointAWorkspace = EndpointABox.Text;
            vm.EndpointBWorkspace = EndpointBBox.Text;
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
            EndpointALabel.Text = Loc.Get("EndpointA");
            EndpointBLabel.Text = Loc.Get("EndpointB");
            BrowseEndpointAButton.Content = Loc.Get("Browse");
            BrowseEndpointBButton.Content = Loc.Get("Browse");
            ApplyEndpointAButton.Content = Loc.Get("ApplyEndpointA");
            ApplyEndpointBButton.Content = Loc.Get("ApplyEndpointB");
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
            if (RealmFilesGrid.Columns.Count > 0)
                return;

            var checkFactory = new FrameworkElementFactory(typeof(CheckBox));
            checkFactory.SetBinding(
                CheckBox.IsCheckedProperty,
                new Binding(nameof(RealmFileRowModel.IsSelected))
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                });
            checkFactory.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            checkFactory.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);

            RealmFilesGrid.Columns.Add(new DataGridTemplateColumn
            {
                Header = string.Empty,
                Width = 40,
                CellTemplate = new DataTemplate { VisualTree = checkFactory },
            });

            RealmFilesGrid.Columns.Add(new DataGridTextColumn
            {
                Header = Loc.Get("ColRealmName"),
                Binding = new Binding(nameof(RealmFileRowModel.DisplayName)) { Mode = BindingMode.OneWay },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                IsReadOnly = true,
            });
            RealmFilesGrid.Columns.Add(new DataGridTextColumn
            {
                Header = Loc.Get("ColOfficialSchema"),
                Binding = new Binding(nameof(RealmFileRowModel.OfficialSchemaDisplay)) { Mode = BindingMode.OneWay },
                Width = 64,
                IsReadOnly = true,
            });
            RealmFilesGrid.Columns.Add(new DataGridTextColumn
            {
                Header = Loc.Get("ColEzSchema"),
                Binding = new Binding(nameof(RealmFileRowModel.EzSchemaDisplay)) { Mode = BindingMode.OneWay },
                Width = 56,
                IsReadOnly = true,
            });
            RealmFilesGrid.Columns.Add(new DataGridTextColumn
            {
                Header = Loc.Get("ColSize"),
                Binding = new Binding(nameof(RealmFileRowModel.SizeDisplay)) { Mode = BindingMode.OneWay },
                Width = 80,
                IsReadOnly = true,
            });
            RealmFilesGrid.Columns.Add(new DataGridTextColumn
            {
                Header = Loc.Get("ColRealmPath"),
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
                        await vm!.RegisterDroppedRealmAsync(action.Path).ConfigureAwait(true);
                        break;

                    case ImportDropActionKind.SetEndpointAWorkspace:
                        vm!.EndpointAWorkspace = action.Path;
                        EndpointABox.Text = action.Path;
                        vm.RefreshRealmFilesCommand.Execute(null);
                        break;

                    case ImportDropActionKind.SetEndpointBWorkspace:
                        vm!.EndpointBWorkspace = action.Path;
                        EndpointBBox.Text = action.Path;
                        vm.RefreshRealmFilesCommand.Execute(null);
                        break;
                }
            }
        }

        private void reportDropError(Exception ex) =>
            vm?.Presenter.MarshalToUi?.Invoke(() => vm.Presenter.StatusMessage.Value = ex.Message);

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            DropZone.DragOver += DropZone_OnDragOver;
            DropZone.Drop += DropZone_OnDrop;
            DropZone.MouseLeftButtonUp += DropZone_OnClick;
            DropZone.MouseEnter += (_, _) => DropZone.Opacity = 0.92;
            DropZone.MouseLeave += (_, _) => DropZone.Opacity = 1;
        }

        private void DropZone_OnClick(object sender, MouseButtonEventArgs e) => BrowseEndpointA_OnClick(sender, e);

        private void ApplyEndpointA_OnClick(object sender, RoutedEventArgs e)
        {
            if (vm == null)
                return;

            applyEndpointPathsFromBoxes();
            vm.ApplyEndpointAPathCommand.Execute(null);
        }

        private void ApplyEndpointB_OnClick(object sender, RoutedEventArgs e)
        {
            if (vm == null)
                return;

            applyEndpointPathsFromBoxes();
            vm.ApplyEndpointBPathCommand.Execute(null);
        }

        private void Refresh_OnClick(object sender, RoutedEventArgs e)
        {
            if (vm == null)
                return;

            applyEndpointPathsFromBoxes();
            vm.RefreshRealmFilesCommand.Execute(null);
        }

        private void BrowseEndpointA_OnClick(object sender, RoutedEventArgs e) => vm?.BrowseEndpointACommand.Execute(null);

        private void BrowseEndpointB_OnClick(object sender, RoutedEventArgs e) => vm?.BrowseEndpointBCommand.Execute(null);

        private void BrowseBackup_OnClick(object sender, RoutedEventArgs e) => vm?.BrowseBackupCommand.Execute(null);

        private void Backup_OnClick(object sender, RoutedEventArgs e) => vm?.BackupSelectedCommand.Execute(null);

        private void RefreshBackups_OnClick(object sender, RoutedEventArgs e) => vm?.RefreshBackupsCommand.Execute(null);

        private void RestoreBackup_OnClick(object sender, RoutedEventArgs e) => vm?.RestoreBackupCommand.Execute(null);

        private void RealmFilesGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (vm == null || RealmFilesGrid.SelectedItem is not RealmFileRowModel row)
                return;

            vm.SelectedRealmId = row.Id;
        }
    }
}
