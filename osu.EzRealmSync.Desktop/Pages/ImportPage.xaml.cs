using System.IO;
using System.Windows.Data;
using osu.EzRealmSync.AppModel.Localization;
using osu.EzRealmSync.Desktop.ViewModels;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.Desktop.Pages
{
    public partial class ImportPage : UserControl
    {
        private ShellViewModel? vm;

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

            if (RealmFilesGrid.ItemsSource == null)
                RealmFilesGrid.ItemsSource = vm.RealmFiles;

            RealmPathBox.Text = vm.SearchDirectory;
            BackupDirBox.Text = vm.BackupDirectory;

            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ShellViewModel.SearchDirectory))
                    Dispatcher.Invoke(() => RealmPathBox.Text = vm.SearchDirectory);
            };
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

            RealmFilesGrid.Columns.Add(new DataGridTextColumn
            {
                Header = Loc.Get("ColRealmName"),
                Binding = new Binding(nameof(RealmFileEntry.DisplayName)),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            });
            RealmFilesGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColOfficialSchema"), Binding = new Binding(nameof(RealmFileEntry.OfficialSchemaDisplay)), Width = 64 });
            RealmFilesGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColEzSchema"), Binding = new Binding(nameof(RealmFileEntry.EzSchemaDisplay)), Width = 56 });
            RealmFilesGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColSize"), Binding = new Binding(nameof(RealmFileEntry.SizeDisplay)), Width = 80 });
            RealmFilesGrid.Columns.Add(new DataGridTextColumn
            {
                Header = Loc.Get("ColRealmPath"),
                Binding = new Binding(nameof(RealmFileEntry.FilePath)),
                Width = new DataGridLength(2, DataGridLengthUnitType.Star),
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

            vm.SearchDirectory = RealmPathBox.Text;
            vm.ApplyRealmPathCommand.Execute(null);
        }

        private void Refresh_OnClick(object sender, RoutedEventArgs e)
        {
            if (vm == null)
                return;

            vm.SearchDirectory = RealmPathBox.Text;
            vm.RefreshRealmFilesCommand.Execute(null);
        }

        private void BrowseRealm_OnClick(object sender, RoutedEventArgs e) => vm?.BrowseRealmLocationCommand.Execute(null);

        private void BrowseBackup_OnClick(object sender, RoutedEventArgs e) => vm?.BrowseBackupCommand.Execute(null);

        private void Backup_OnClick(object sender, RoutedEventArgs e) => vm?.BackupSelectedCommand.Execute(null);

        private void RealmFilesGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (vm == null || RealmFilesGrid.SelectedItem is not RealmFileEntry entry)
                return;

            vm.SelectedRealmId = entry.Id;
        }
    }
}
