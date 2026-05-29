using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    public static class RealmSyncPathHelper
    {
        public static bool TryValidateRealmFileAccessible(string realmFilePath, out string? error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(realmFilePath))
            {
                error = "未指定 client.realm 路径。";
                return false;
            }

            string fullPath = Path.GetFullPath(realmFilePath);

            if (!File.Exists(fullPath))
            {
                error = $"找不到 Realm 文件：{fullPath}";
                return false;
            }

            try
            {
                using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }
            catch (IOException ex)
            {
                error = $"无法读取 Realm 文件（可能正被游戏占用）：{ex.Message}";
                return false;
            }

            return true;
        }

        public static bool SharedFilesDirectoriesMatch(string? ezWorkspace, string? officialWorkspace)
        {
            if (!RealmWorkspacePaths.TryResolveFilesDirectory(ezWorkspace, out string ezFiles))
                return false;

            if (!RealmWorkspacePaths.TryResolveFilesDirectory(officialWorkspace, out string officialFiles))
                return false;

            return string.Equals(Path.GetFullPath(ezFiles), Path.GetFullPath(officialFiles), StringComparison.OrdinalIgnoreCase);
        }
    }
}
