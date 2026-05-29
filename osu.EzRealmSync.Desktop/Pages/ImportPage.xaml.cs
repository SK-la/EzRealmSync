using System.IO;
using System.Windows.Data;
using osu.EzRealmSync.AppModel;
using osu.EzRealmSync.AppModel.Localization;
using osu.EzRealmSync.Desktop.Helpers;
using osu.EzRealmSync.Desktop.ViewModels;

namespace osu.EzRealmSync.Desktop.Pages
{
    public partial class ImportPage : UserControl
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

            RealmPathBox.Text = vm.SearchDirectory;
            BackupDirBox.Text = vm.BackupDirectory;

            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ShellViewModel.SearchDirectory))
                    Dispatcher.Invoke(() => RealmPathBox.Text = vm.SearchDirectory);

                if (e.PropertyName == nameof(ShellViewModel.BackupDirectory))
                    Dispatcher.Invoke(() => BackupDirBox.Text = vm.BackupDirectory);

                if (e.PropertyName == nameof(ShellViewModel.RealmFileRows))
                    Dispatcher.Invoke(() => RealmFilesGrid.ItemsSource = vm!.RealmFileRows);
            };

            vm.Presenter.RealmFilesChanged += () => Dispatcher.Invoke(() => RealmFilesGrid.ItemsSource = vm!.RealmFileRows);

            RealmPathBox.LostFocus += (_, _) => applyPathFromBox();
            BackupDirBox.LostFocus += (_, _) => applyBackupFromBox();
        }

        private void configureRealmGrid()
        {
            if (realmGridConfigured || vm == null)
                return;

            realmGridConfigured = true;
            RealmFilesGrid.ItemsSource = vm.RealmFileRows;

            CheckableDataGridHelper.Configure<RealmFileRowModel>(
                RealmFilesGrid,
                () => vm.RealmFileRows,
                (rows, check) => vm.Presenter.SetRealmFileRowsChecked(rows, check),
                () => vm.Presenter.InvertRealmFileRowChecks(),
                rows => vm.Presenter.DeleteRealmFileRowsAsync(rows));
        }

        private void applyPathFromBox()
        {
            if (vm == null)
                return;

            vm.SearchDirectory = RealmPathBox.Text;
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
            RealmPathLabel.Text = Loc.Get("RealmLocation");
            BrowseRealmButton.Content = Loc.Get("Browse");
            ApplyPathButton.Content = Loc.Get("ApplyRealmPath");
            RefreshButton.Content = Loc.Get("RefreshList");
            BackupDirLabel.Text = Loc.Get("BackupDirectory");
            BrowseBackupButton.Content = Loc.Get("Browse");
            BackupButton.Content = Loc.Get("BackupSelected");
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

        private async void DropZone_OnDrop(object sender, DragEventArgs e)
        {
            if (vm == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
                return;

            foreach (string file in files)
            {
                if (file.EndsWith(".realm", StringComparison.OrdinalIgnoreCase))
                    await vm.RegisterDroppedRealmAsync(file);
                else if (Directory.Exists(file))
                {
                    vm.SearchDirectory = file;
                    RealmPathBox.Text = file;
                    vm.RefreshRealmFilesCommand.Execute(null);
                }
            }
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            DropZone.DragOver += DropZone_OnDragOver;
            DropZone.Drop += DropZone_OnDrop;
            DropZone.MouseLeftButtonUp += DropZone_OnClick;
            DropZone.MouseEnter += (_, _) => DropZone.Opacity = 0.92;
            DropZone.MouseLeave += (_, _) => DropZone.Opacity = 1;
        }

        private void DropZone_OnClick(object sender, MouseButtonEventArgs e) => BrowseRealm_OnClick(sender, e);

        private void ApplyPath_OnClick(object sender, RoutedEventArgs e)
        {
            if (vm == null)
                return;

            applyPathFromBox();
            vm.ApplyRealmPathCommand.Execute(null);
        }

        private void Refresh_OnClick(object sender, RoutedEventArgs e)
        {
            if (vm == null)
                return;

            applyPathFromBox();
            vm.RefreshRealmFilesCommand.Execute(null);
        }

        private void BrowseRealm_OnClick(object sender, RoutedEventArgs e) => vm?.BrowseRealmLocationCommand.Execute(null);

        private void BrowseBackup_OnClick(object sender, RoutedEventArgs e) => vm?.BrowseBackupCommand.Execute(null);

        private void Backup_OnClick(object sender, RoutedEventArgs e) => vm?.BackupSelectedCommand.Execute(null);

        private void RealmFilesGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (vm == null || RealmFilesGrid.SelectedItem is not RealmFileRowModel row)
                return;

            vm.SelectedRealmId = row.Id;
        }
    }
}
