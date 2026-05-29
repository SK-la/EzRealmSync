using System.Collections.ObjectModel;
using System.IO;
using osu.EzRealmSync.AppModel.Localization;

namespace osu.EzRealmSync.Desktop
{
    public partial class FolderPickerWindow : FluentWindow
    {
        private string currentPath = string.Empty;

        public string? SelectedPath { get; private set; }

        public FolderPickerWindow(string? initialPath, string title)
        {
            InitializeComponent();
            ApplicationThemeManager.Apply(this);
            Title = title;
            PickerTitleBar.Title = title;
            UpButton.Content = Loc.Get("FolderPickerUp");
            CancelButton.Content = Loc.Get("FolderPickerCancel");
            SelectButton.Content = Loc.Get("FolderPickerSelect");

            var start = initialPath;
            if (!string.IsNullOrWhiteSpace(start) && Directory.Exists(start))
                start = File.Exists(start) ? Path.GetDirectoryName(start) : start;

            if (string.IsNullOrWhiteSpace(start) || !Directory.Exists(start))
                start = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            navigateTo(start!);
        }

        private void navigateTo(string path)
        {
            currentPath = Path.GetFullPath(path);
            refreshBreadcrumbs();
            refreshList();
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
                var label = string.IsNullOrEmpty(dir.Name) ? dir.FullName.TrimEnd(Path.DirectorySeparatorChar) : dir.Name;
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

        private void refreshList()
        {
            var entries = new ObservableCollection<FolderEntry>();

            try
            {
                var parent = Directory.GetParent(currentPath);
                if (parent != null)
                    entries.Add(new FolderEntry("..", parent.FullName, isParent: true));

                foreach (var dir in Directory.GetDirectories(currentPath).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        entries.Add(new FolderEntry(Path.GetFileName(dir), dir));
                    }
                    catch
                    {
                        // 跳过无权限目录
                    }
                }
            }
            catch
            {
                // 当前路径不可读时保持列表为空
            }

            FolderList.ItemsSource = entries;
            FolderList.DisplayMemberPath = nameof(FolderEntry.Name);
        }

        private void enterSelected()
        {
            if (FolderList.SelectedItem is not FolderEntry entry)
                return;

            if (Directory.Exists(entry.FullPath))
                navigateTo(entry.FullPath);
        }

        private void PathBreadcrumb_OnItemClicked(object sender, RoutedEventArgs e)
        {
            if (e is not BreadcrumbBarItemClickedEventArgs { Item: BreadcrumbBarItem { Tag: string path } })
                return;

            if (Directory.Exists(path))
                navigateTo(path);
        }

        private void FolderList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e) => enterSelected();

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

        private void SelectButton_OnClick(object sender, RoutedEventArgs e)
        {
            SelectedPath = currentPath;
            DialogResult = true;
            Close();
        }

        private sealed class FolderEntry
        {
            public FolderEntry(string name, string fullPath, bool isParent = false)
            {
                Name = name;
                FullPath = fullPath;
                IsParent = isParent;
            }

            public string Name { get; }
            public string FullPath { get; }
            public bool IsParent { get; }
        }
    }
}
