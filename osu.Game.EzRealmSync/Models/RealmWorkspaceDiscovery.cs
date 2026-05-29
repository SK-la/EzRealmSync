namespace osu.Game.EzRealmSync.Models
{
    /// <summary>在 A/B 工作区路径下发现 <c>*.realm</c> 文件（去重）。</summary>
    public static class RealmWorkspaceDiscovery
    {
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
    }
}
