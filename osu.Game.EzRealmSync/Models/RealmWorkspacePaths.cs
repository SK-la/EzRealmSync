namespace osu.Game.EzRealmSync.Models
{
    /// <summary>
    /// osu!lazer 存储布局：<c>{storage}/data/*.realm</c> 与 <c>{storage}/files/</c>。
    /// </summary>
    public static class RealmWorkspacePaths
    {
        public static IReadOnlyList<string> FindRealmFiles(string? workspacePath)
        {
            if (string.IsNullOrWhiteSpace(workspacePath))
                return Array.Empty<string>();

            string path = Path.GetFullPath(workspacePath.Trim());

            if (File.Exists(path) && path.EndsWith(".realm", StringComparison.OrdinalIgnoreCase))
                return new[] { path };

            if (!Directory.Exists(path))
                return Array.Empty<string>();

            var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string file in Directory.EnumerateFiles(path, "*.realm", SearchOption.TopDirectoryOnly))
                results.Add(file);

            string dataDir = Path.Combine(path, "data");

            if (Directory.Exists(dataDir))
            {
                foreach (string file in Directory.EnumerateFiles(dataDir, "*.realm", SearchOption.TopDirectoryOnly))
                    results.Add(file);
            }

            return results.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static bool TryResolveFilesDirectory(string? workspacePath, out string filesDirectory)
        {
            filesDirectory = string.Empty;

            if (string.IsNullOrWhiteSpace(workspacePath))
                return false;

            string path = Path.GetFullPath(workspacePath.Trim());

            if (File.Exists(path))
                path = Path.GetDirectoryName(path) ?? path;

            if (!Directory.Exists(path))
                return false;

            string direct = Path.Combine(path, "files");

            if (Directory.Exists(direct))
            {
                filesDirectory = direct;
                return true;
            }

            if (string.Equals(Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), "data", StringComparison.OrdinalIgnoreCase))
            {
                string sibling = Path.Combine(Directory.GetParent(path)?.FullName ?? path, "files");

                if (Directory.Exists(sibling))
                {
                    filesDirectory = sibling;
                    return true;
                }
            }

            string? parent = Directory.GetParent(path)?.FullName;

            if (parent != null)
            {
                string parentFiles = Path.Combine(parent, "files");

                if (Directory.Exists(parentFiles))
                {
                    filesDirectory = parentFiles;
                    return true;
                }
            }

            return false;
        }

        public static bool WorkspaceHasFilesFolder(string? workspacePath) => TryResolveFilesDirectory(workspacePath, out _);

        public static string ResolveStorageRoot(string realmFilePath)
        {
            string fullPath = Path.GetFullPath(realmFilePath);
            string? directory = Path.GetDirectoryName(fullPath);

            if (directory == null)
                return fullPath;

            if (string.Equals(Path.GetFileName(directory), "data", StringComparison.OrdinalIgnoreCase))
                return Directory.GetParent(directory)?.FullName ?? directory;

            return directory;
        }

        public static string ResolveDataDirectory(string realmFilePath) => Path.GetDirectoryName(Path.GetFullPath(realmFilePath)) ?? string.Empty;
    }
}
