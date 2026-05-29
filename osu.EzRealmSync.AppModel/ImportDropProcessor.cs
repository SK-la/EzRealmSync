namespace osu.EzRealmSync.AppModel
{
    public enum ImportDropActionKind
    {
        RegisterRealm,
        SetSearchDirectory,
    }

    public readonly record struct ImportDropAction(ImportDropActionKind Kind, string Path);

    /// <summary>
    /// 解析拖放到导入页的路径（与 WPF 解耦，可单测）。
    /// </summary>
    public static class ImportDropProcessor
    {
        public static IReadOnlyList<ImportDropAction> ParseDroppedPaths(IEnumerable<string> paths)
        {
            var actions = new List<ImportDropAction>();

            foreach (string path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                string fullPath = Path.GetFullPath(path.Trim());

                if (fullPath.EndsWith(".realm", StringComparison.OrdinalIgnoreCase))
                {
                    actions.Add(new ImportDropAction(ImportDropActionKind.RegisterRealm, fullPath));
                    continue;
                }

                if (Directory.Exists(fullPath))
                    actions.Add(new ImportDropAction(ImportDropActionKind.SetSearchDirectory, fullPath));
            }

            return actions;
        }
    }
}
