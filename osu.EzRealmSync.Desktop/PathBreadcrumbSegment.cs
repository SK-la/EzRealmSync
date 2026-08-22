namespace osu.EzRealmSync.Desktop
{
    /// <summary>路径分段，供 <see cref="Wpf.Ui.Controls.BreadcrumbBar"/> ItemsSource 绑定。</summary>
    public sealed class PathBreadcrumbSegment
    {
        public PathBreadcrumbSegment(string name, string fullPath)
        {
            Name = name;
            FullPath = fullPath;
        }

        public string Name { get; }

        public string FullPath { get; }
    }
}
