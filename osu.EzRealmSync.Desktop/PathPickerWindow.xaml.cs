using System.Collections.ObjectModel;
using System.IO;
using osu.EzRealmSync.AppModel.Localization;
using osu.EzRealmSync.Desktop.Services;

namespace osu.EzRealmSync.Desktop
{
    public partial class PathPickerWindow
    {
        private readonly PathPickerMode mode;
        private string currentPath = string.Empty;

        public string? SelectedPath { get; private set; }

        public PathPickerWindow(PathPickerMode mode, string? initialPath, string title)
        {
            this.mode = mode;
            InitializeComponent();
            ApplicationThemeManager.Apply(this);
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
            var panelFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.StackPanel));
            panelFactory.SetValue(System.Windows.Controls.StackPanel.OrientationProperty, Orientation.Horizontal);
            panelFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 6, 4, 6));

            var iconFactory = new FrameworkElementFactory(typeof(SymbolIcon));
            iconFactory.SetBinding(SymbolIcon.SymbolProperty, new System.Windows.Data.Binding(nameof(PlaceEntry.Icon)));
            iconFactory.SetValue(Control.FontSizeProperty, 16.0);
            iconFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 10, 0));
            iconFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

            var textFactory = new FrameworkElementFactory(typeof(TextBlock));
            textFactory.SetBinding(System.Windows.Controls.TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(PlaceEntry.Name)));
            textFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
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
            PathBreadcrumb.Items.Clear();

            if (string.IsNullOrEmpty(currentPath))
                return;

            var segments = new List<(string label, string path)>();
            var dir = new DirectoryInfo(currentPath);

            while (dir != null)
            {
                string label = string.IsNullOrEmpty(dir.Name)
                    ? dir.FullName.TrimEnd(Path.DirectorySeparatorChar)
                    : dir.Name;
                segments.Insert(0, (label, dir.FullName));
                dir = dir.Parent;
            }

            for (int i = 0; i < segments.Count; i++)
            {
                PathBreadcrumb.Items.Add(new BreadcrumbBarItem
                {
                    Content = segments[i].label,
                    Tag = segments[i].path,
                    IsLast = i == segments.Count - 1,
                });
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
                        entries.Add(FileSystemEntry.RealmFile(file));
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
            DialogResult = true;
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

            if (mode == PathPickerMode.RealmPath && entry.IsRealmFile)
                commitSelection(entry.FullPath);
        }

        private void PlacesList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PlacesList.SelectedItem is PlaceEntry place && Directory.Exists(place.FullPath))
                navigateTo(place.FullPath);
        }

        private void PathBreadcrumb_OnItemClicked(object sender, RoutedEventArgs e)
        {
            if (e is not BreadcrumbBarItemClickedEventArgs { Item: BreadcrumbBarItem { Tag: string path } })
                return;

            if (Directory.Exists(path))
                navigateTo(path);
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
            DialogResult = false;
            Close();
        }

        private void SelectFolderButton_OnClick(object sender, RoutedEventArgs e) => commitSelection(currentPath);

        private void SelectButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (EntryList.SelectedItem is FileSystemEntry { IsRealmFile: true } file)
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
            private FileSystemEntry(string name, string fullPath, bool isDirectory, bool isRealmFile, bool isParent)
            {
                Name = name;
                FullPath = fullPath;
                IsDirectory = isDirectory;
                IsRealmFile = isRealmFile;
                IsParent = isParent;
            }

            public string Name { get; }
            public string FullPath { get; }
            public bool IsDirectory { get; }
            public bool IsRealmFile { get; }
            public bool IsParent { get; }

            public string TypeLabel => IsParent ? ".." : IsDirectory ? Loc.Get("PathPickerTypeFolder") : Loc.Get("PathPickerTypeRealm");

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

            public static FileSystemEntry Parent(string fullPath) => new FileSystemEntry("..", fullPath, true, false, true);

            public static FileSystemEntry FromDirectory(string fullPath) => new FileSystemEntry(Path.GetFileName(fullPath), fullPath, true, false, false);

            public static FileSystemEntry RealmFile(string fullPath) => new FileSystemEntry(Path.GetFileName(fullPath), fullPath, false, true, false);
        }
    }
}
