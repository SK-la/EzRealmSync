namespace osu.Game.EzRealmSync.Models
{
    /// <summary>在导入页指定的 osu! 存储根目录下发现 Realm 与共享 <c>files/</c>。</summary>
    public static class RealmWorkspaceDiscovery
    {
        public static IReadOnlyList<string> FindRealmFilesInSearchDirectory(string? searchDirectory) =>
            RealmWorkspacePaths.FindRealmFiles(searchDirectory);

        /// <summary>
        /// 将用户输入规范为 osu! 存储根目录（根目录或 <c>data/</c> 下的 <c>*.realm</c>，以及共享 <c>files/</c>）。
        /// </summary>
        public static string NormalizeStorageRoot(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            string fullPath = Path.GetFullPath(path.Trim());

            if (File.Exists(fullPath) && fullPath.EndsWith(".realm", StringComparison.OrdinalIgnoreCase))
                return RealmWorkspacePaths.ResolveStorageRoot(fullPath);

            if (!Directory.Exists(fullPath))
                return fullPath;

            string name = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            if (string.Equals(name, "data", StringComparison.OrdinalIgnoreCase))
                return Directory.GetParent(fullPath)?.FullName ?? fullPath;

            return fullPath;
        }

        /// <summary>解析导入目录下所有 Realm 共用的 <c>files/</c>（仅依据存储根目录，不看单个 .realm 路径）。</summary>
        public static bool TryResolveSharedFilesDirectory(string? searchDirectory, out string filesDirectory) =>
            RealmWorkspacePaths.TryResolveFilesDirectory(NormalizeStorageRoot(searchDirectory), out filesDirectory);

        public static bool TryResolveStorageRoot(string? searchDirectory, out string storageRoot)
        {
            storageRoot = NormalizeStorageRoot(searchDirectory);

            if (string.IsNullOrWhiteSpace(storageRoot) || !Directory.Exists(storageRoot))
                return false;

            return true;
        }

        public static IReadOnlyList<string> FindRealmFilesInWorkspaces(string? endpointAWorkspace, string? endpointBWorkspace)
        {
            var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string? workspace in new[] { endpointAWorkspace, endpointBWorkspace })
            {
                if (string.IsNullOrWhiteSpace(workspace))
                    continue;

                foreach (string file in RealmWorkspacePaths.FindRealmFiles(workspace))
                    results.Add(Path.GetFullPath(file));
            }

            return results.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static bool AnyWorkspaceHasFilesFolder(string? endpointAWorkspace, string? endpointBWorkspace) =>
            RealmWorkspacePaths.WorkspaceHasFilesFolder(endpointAWorkspace)
            || RealmWorkspacePaths.WorkspaceHasFilesFolder(endpointBWorkspace);

        public static bool TryResolveFilesDirectory(string? endpointAWorkspace, string? endpointBWorkspace, out string filesDirectory)
        {
            if (RealmWorkspacePaths.TryResolveFilesDirectory(endpointAWorkspace, out filesDirectory))
                return true;

            return RealmWorkspacePaths.TryResolveFilesDirectory(endpointBWorkspace, out filesDirectory);
        }

        /// <summary>优先使用导入页存储根目录的共享 <c>files/</c>；仅在未设置导入目录时回退到 Realm 文件旁路径。</summary>
        public static bool TryResolveFilesDirectoryForRealm(string? searchDirectory, string? realmFilePath, out string filesDirectory)
        {
            if (TryResolveSharedFilesDirectory(searchDirectory, out filesDirectory))
                return true;

            if (!string.IsNullOrWhiteSpace(realmFilePath))
            {
                string? realmDir = Path.GetDirectoryName(Path.GetFullPath(realmFilePath));

                if (realmDir != null && RealmWorkspacePaths.TryResolveFilesDirectory(realmDir, out filesDirectory))
                    return true;
            }

            filesDirectory = string.Empty;
            return false;
        }
    }
}
