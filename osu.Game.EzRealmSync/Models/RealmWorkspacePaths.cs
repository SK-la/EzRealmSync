namespace osu.Game.EzRealmSync.Models
{
    /// <summary>
    /// osu! 存储布局：共享 <c>{storage}/files/</c>，Realm 可在根目录（如 Ez2Lazer）或 <c>{storage}/data/</c>（官方 lazer）。
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

        /// <summary>
        /// 供 <see cref="NativeStorage"/> 打开的相对路径（如 <c>data\client.realm</c>），
        /// 不可仅用 <see cref="Path.GetFileName"/>（会丢失 <c>data\</c> 段）。
        /// </summary>
        public static string ResolveStorageRelativeRealmPath(string realmFilePath)
        {
            string fullPath = Path.GetFullPath(realmFilePath);
            string storageRoot = ResolveStorageRoot(fullPath);

            if (string.Equals(fullPath, storageRoot, StringComparison.OrdinalIgnoreCase))
                return Path.GetFileName(fullPath);

            return Path.GetRelativePath(storageRoot, fullPath);
        }

        public static string ResolveDataDirectory(string realmFilePath) => Path.GetDirectoryName(Path.GetFullPath(realmFilePath)) ?? string.Empty;

        public static string ResolveClientRealmPath(string? workspacePath)
        {
            if (string.IsNullOrWhiteSpace(workspacePath))
                return string.Empty;

            string root = Path.GetFullPath(workspacePath.Trim());
            var files = FindRealmFiles(root);

            string? ezSidecar = files
                .Select(path => (path, version: tryParseClientRealmVersion(Path.GetFileName(path))))
                .Where(entry => entry.version is >= 1000)
                .OrderByDescending(entry => entry.version)
                .Select(entry => entry.path)
                .FirstOrDefault();

            if (ezSidecar != null)
                return ezSidecar;

            string? client = files.FirstOrDefault(f => Path.GetFileName(f).Equals("client.realm", StringComparison.OrdinalIgnoreCase));
            if (client != null)
                return client;

            string? legacySidecar = files
                .Select(path => (path, version: tryParseClientRealmVersion(Path.GetFileName(path))))
                .Where(entry => entry.version is > 0 and < 1000)
                .OrderByDescending(entry => entry.version)
                .Select(entry => entry.path)
                .FirstOrDefault();

            if (legacySidecar != null)
                return legacySidecar;

            string atRoot = Path.Combine(root, "client.realm");
            if (File.Exists(atRoot))
                return atRoot;

            return Path.Combine(root, "data", "client.realm");
        }

        private static int? tryParseClientRealmVersion(string fileName)
        {
            const string prefix = "client_";
            const string suffix = ".realm";

            if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || !fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string versionText = fileName.Substring(prefix.Length, fileName.Length - prefix.Length - suffix.Length);

            return int.TryParse(versionText, out int version) ? version : null;
        }
    }
}
