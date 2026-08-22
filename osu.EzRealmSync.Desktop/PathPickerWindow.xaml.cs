using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Data;
using osu.EzRealmSync.AppModel.Localization;
using osu.EzRealmSync.Desktop.Services;
using StackPanel = System.Windows.Controls.StackPanel;

namespace osu.EzRealmSync.Desktop
{
    public partial class PathPickerWindow
    {
        private readonly PathPickerMode mode;
        private string currentPath = string.Empty;
        private readonly ObservableCollection<PathBreadcrumbSegment> breadcrumbSegments = new();

        public string? SelectedPath { get; private set; }

        public bool IsConfirmed { get; private set; }

        public PathPickerWindow(PathPickerMode mode, string? initialPath, string title)
        {
            this.mode = mode;
            InitializeComponent();
            ApplicationThemeManager.Apply(this);
            PathBreadcrumb.ItemsSource = breadcrumbSegments;
            Title = title;
            PickerTitleBar.Title = title;
            refreshChrome();

            string start = resolveInitialDirectory(initialPath);
            navigateTo(start);
            refreshPlaces();
        }

        private static string resolveInitialDirectory(string? initialPath)
        {
            if (!string.IsNullOrWhiteSpace(initialPath))
            {
                if (File.Exists(initialPath))
                    return Path.GetDirectoryName(initialPath) ?? initialPath;

                if (Directory.Exists(initialPath))
                    return initialPath;
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        private void refreshChrome()
        {
            UpButton.Content = Loc.Get("FolderPickerUp");
            RefreshButton.Content = Loc.Get("PathPickerRefresh");
            GoButton.Content = Loc.Get("PathPickerGo");
            PlacesLabel.Text = Loc.Get("PathPickerPlaces");
            CancelButton.Content = Loc.Get("FolderPickerCancel");
            SelectButton.Content = mode == PathPickerMode.Folder ? Loc.Get("FolderPickerSelect") : Loc.Get("PathPickerSelect");
            SelectFolderButton.Content = Loc.Get("PathPickerSelectFolder");
            SelectFolderButton.Visibility = mode == PathPickerMode.RealmPath ? Visibility.Visible : Visibility.Collapsed;
            NameColumn.Header = Loc.Get("PathPickerColName");
            TypeColumn.Header = Loc.Get("PathPickerColType");
            ModifiedColumn.Header = Loc.Get("PathPickerColModified");
            SizeColumn.Header = Loc.Get("PathPickerColSize");
            updateSelectionHint();
        }

        private void refreshPlaces()
        {
            var places = new ObservableCollection<PlaceEntry>
            {
                new PlaceEntry(Loc.Get("PathPickerDesktop"), Environment.GetFolderPath(Environment.SpecialFolder.Desktop), SymbolRegular.Desktop24),
                new PlaceEntry(Loc.Get("PathPickerDocuments"), Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), SymbolRegular.Document24),
                new PlaceEntry(Loc.Get("PathPickerDownloads"), getDownloadsPath(), SymbolRegular.ArrowDownload24),
            };

            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                string label = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                    ? drive.Name.TrimEnd('\\')
                    : $"{drive.VolumeLabel} ({drive.Name.TrimEnd('\\')})";

                places.Add(new PlaceEntry(label, drive.Name, SymbolRegular.HardDrive24));
            }

            PlacesList.ItemsSource = places;
            PlacesList.ItemTemplate = createPlaceTemplate();
        }

        private static string getDownloadsPath()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string downloads = Path.Combine(userProfile, "Downloads");
            return Directory.Exists(downloads) ? downloads : userProfile;
        }

        private static DataTemplate createPlaceTemplate()
        {
            var template = new DataTemplate(typeof(PlaceEntry));
            var panelFactory = new FrameworkElementFactory(typeof(StackPanel));
            panelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            panelFactory.SetValue(MarginProperty, new Thickness(4, 6, 4, 6));

            var iconFactory = new FrameworkElementFactory(typeof(SymbolIcon));
            iconFactory.SetBinding(SymbolIcon.SymbolProperty, new Binding(nameof(PlaceEntry.Icon)));
            iconFactory.SetValue(FontSizeProperty, 16.0);
            iconFactory.SetValue(MarginProperty, new Thickness(0, 0, 10, 0));
            iconFactory.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);

            var textFactory = new FrameworkElementFactory(typeof(TextBlock));
            textFactory.SetBinding(System.Windows.Controls.TextBlock.TextProperty, new Binding(nameof(PlaceEntry.Name)));
            textFactory.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            textFactory.SetValue(System.Windows.Controls.TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);

            panelFactory.AppendChild(iconFactory);
            panelFactory.AppendChild(textFactory);
            template.VisualTree = panelFactory;
            return template;
        }

        private void navigateTo(string path)
        {
            if (!Directory.Exists(path))
                return;

            currentPath = Path.GetFullPath(path);
            PathBox.Text = currentPath;
            refreshBreadcrumbs();
            refreshEntries();
            updateSelectionHint();
        }

        private void refreshBreadcrumbs()
        {
            breadcrumbSegments.Clear();

            if (string.IsNullOrEmpty(currentPath))
                return;

            var dir = new DirectoryInfo(currentPath);

            while (dir != null)
            {
                string label = string.IsNullOrEmpty(dir.Name)
                    ? dir.FullName.TrimEnd(Path.DirectorySeparatorChar)
                    : dir.Name;
                breadcrumbSegments.Insert(0, new PathBreadcrumbSegment(label, dir.FullName));
                dir = dir.Parent;
            }
        }

        private void refreshEntries()
        {
            var entries = new ObservableCollection<FileSystemEntry>();

            try
            {
                var parent = Directory.GetParent(currentPath);
                if (parent != null)
                    entries.Add(FileSystemEntry.Parent(parent.FullName));

                foreach (string dir in Directory.GetDirectories(currentPath).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        entries.Add(FileSystemEntry.FromDirectory(dir));
                    }
                    catch
                    {
                        // 跳过无权限目录
                    }
                }

                if (mode == PathPickerMode.RealmPath)
                {
                    foreach (string file in Directory.GetFiles(currentPath, "*.realm").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                        entries.Add(FileSystemEntry.SelectableFile(file, Loc.Get("PathPickerTypeRealm")));
                }
                else if (mode == PathPickerMode.CollectionDb)
                {
                    foreach (string file in Directory.GetFiles(currentPath, "*.db").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                        entries.Add(FileSystemEntry.SelectableFile(file, Loc.Get("PathPickerTypeCollectionDb")));
                }
            }
            catch
            {
                // 当前路径不可读
            }

            EntryList.ItemsSource = entries;
        }

        private void updateSelectionHint()
        {
            if (EntryList.SelectedItem is FileSystemEntry entry)
            {
                SelectionHint.Text = entry.FullPath;
                return;
            }

            SelectionHint.Text = currentPath;
        }

        private void commitSelection(string path)
        {
            SelectedPath = path;
            IsConfirmed = true;
            Close();
        }

        private void tryNavigateFromPathBox()
        {
            string text = PathBox.Text.Trim();
            if (string.IsNullOrEmpty(text))
                return;

            if (Directory.Exists(text))
            {
                navigateTo(text);
                return;
            }

            if (File.Exists(text))
            {
                if (mode == PathPickerMode.RealmPath && text.EndsWith(".realm", StringComparison.OrdinalIgnoreCase))
                    commitSelection(text);
                else if (mode == PathPickerMode.CollectionDb && text.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                    commitSelection(text);
                else
                    navigateTo(Path.GetDirectoryName(text) ?? text);
            }
        }

        private void enterSelectedEntry()
        {
            if (EntryList.SelectedItem is not FileSystemEntry entry)
                return;

            if (entry.IsDirectory)
            {
                navigateTo(entry.FullPath);
                return;
            }

            if (mode is PathPickerMode.RealmPath or PathPickerMode.CollectionDb && entry.IsSelectableFile)
                commitSelection(entry.FullPath);
        }

        private void PathBreadcrumb_OnItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs e)
        {
            if (e.Item is not PathBreadcrumbSegment segment)
                return;

            // 当前目录（最后一项）无需跳转
            if (e.Index >= breadcrumbSegments.Count - 1)
                return;

            if (!Directory.Exists(segment.FullPath))
                return;

            string target = segment.FullPath;
            // 须在 BreadcrumbBar 内部 Click 结束后再改 ItemsSource，否则 ContainerFromItem 会拿到 null
            Dispatcher.BeginInvoke(() => navigateTo(target), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void PlacesList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PlacesList.SelectedItem is PlaceEntry place && Directory.Exists(place.FullPath))
                navigateTo(place.FullPath);
        }

        private void EntryList_OnSelectionChanged(object sender, SelectionChangedEventArgs e) => updateSelectionHint();

        private void EntryList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e) => enterSelectedEntry();

        private void PathBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                tryNavigateFromPathBox();
                e.Handled = true;
            }
        }

        private void GoButton_OnClick(object sender, RoutedEventArgs e) => tryNavigateFromPathBox();

        private void RefreshButton_OnClick(object sender, RoutedEventArgs e) => refreshEntries();

        private void UpButton_OnClick(object sender, RoutedEventArgs e)
        {
            var parent = Directory.GetParent(currentPath);
            if (parent != null)
                navigateTo(parent.FullName);
        }

        private void CancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            Close();
        }

        private void SelectFolderButton_OnClick(object sender, RoutedEventArgs e) => commitSelection(currentPath);

        private void SelectButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (EntryList.SelectedItem is FileSystemEntry { IsSelectableFile: true } file)
            {
                commitSelection(file.FullPath);
                return;
            }

            if (mode == PathPickerMode.Folder || mode == PathPickerMode.RealmPath)
                commitSelection(currentPath);
        }

        private sealed class PlaceEntry
        {
            public PlaceEntry(string name, string fullPath, SymbolRegular icon)
            {
                Name = name;
                FullPath = fullPath;
                Icon = icon;
            }

            public string Name { get; }
            public string FullPath { get; }
            public SymbolRegular Icon { get; }
        }

        private sealed class FileSystemEntry
        {
            private FileSystemEntry(string name, string fullPath, bool isDirectory, bool isSelectableFile, bool isParent, string? fileTypeLabel)
            {
                Name = name;
                FullPath = fullPath;
                IsDirectory = isDirectory;
                IsSelectableFile = isSelectableFile;
                IsParent = isParent;
                FileTypeLabel = fileTypeLabel;
            }

            public string Name { get; }
            public string FullPath { get; }
            public bool IsDirectory { get; }
            public bool IsSelectableFile { get; }
            public bool IsParent { get; }
            private string? FileTypeLabel { get; }

            public string TypeLabel => IsParent ? ".." : IsDirectory ? Loc.Get("PathPickerTypeFolder") : FileTypeLabel ?? Loc.Get("PathPickerTypeRealm");

            public string ModifiedLabel
            {
                get
                {
                    try
                    {
                        var when = IsDirectory ? Directory.GetLastWriteTime(FullPath) : File.GetLastWriteTime(FullPath);
                        return when.ToString("g");
                    }
                    catch
                    {
                        return string.Empty;
                    }
                }
            }

            public string SizeLabel
            {
                get
                {
                    if (IsDirectory || IsParent)
                        return string.Empty;

                    try
                    {
                        long bytes = new FileInfo(FullPath).Length;
                        return bytes < 1024 * 1024
                            ? $"{bytes / 1024.0:0.#} KB"
                            : $"{bytes / 1024.0 / 1024.0:0.#} MB";
                    }
                    catch
                    {
                        return string.Empty;
                    }
                }
            }

            public static FileSystemEntry Parent(string fullPath) => new FileSystemEntry("..", fullPath, true, false, true, null);

            public static FileSystemEntry FromDirectory(string fullPath) => new FileSystemEntry(Path.GetFileName(fullPath), fullPath, true, false, false, null);

            public static FileSystemEntry SelectableFile(string fullPath, string typeLabel) =>
                new FileSystemEntry(Path.GetFileName(fullPath), fullPath, false, true, false, typeLabel);
        }
    }
}
