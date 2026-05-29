using osu.Game.EzRealmSync.Realm;

namespace osu.Game.EzRealmSync.IO
{
    /// <summary>
    /// 创建 Realm 文件的时间戳备份；不覆盖已有备份，不修改源文件。
    /// </summary>
    public static class RealmFileBackup
    {
        public static string CreateTimestampedCopy(string realmFilePath, string backupDirectory, DateTimeOffset? timestamp = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(realmFilePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(backupDirectory);

            string sourcePath = Path.GetFullPath(realmFilePath);

            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Realm file not found.", sourcePath);

            Directory.CreateDirectory(backupDirectory);

            string stamp = (timestamp ?? DateTimeOffset.Now).ToString("yyyyMMdd_HHmmss");
            string fileName = Path.GetFileName(sourcePath);
            string backupPath = Path.Combine(
                backupDirectory,
                $"{Path.GetFileNameWithoutExtension(fileName)}_{stamp}{Path.GetExtension(fileName)}");

            if (File.Exists(backupPath))
                throw new IOException($"Backup already exists: {backupPath}");

            File.Copy(sourcePath, backupPath, overwrite: false);

            return backupPath;
        }

        /// <summary>
        /// 用备份文件覆盖目标 Realm；可选在覆盖前为当前目标再建一份时间戳备份。
        /// </summary>
        public static void RestoreOverTarget(string backupFilePath, string targetRealmFilePath, string? safetyBackupDirectory = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(backupFilePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(targetRealmFilePath);

            string backupPath = Path.GetFullPath(backupFilePath);
            string targetPath = Path.GetFullPath(targetRealmFilePath);

            if (!File.Exists(backupPath))
                throw new FileNotFoundException("Backup file not found.", backupPath);

            if (!File.Exists(targetPath))
                throw new FileNotFoundException("Target Realm file not found.", targetPath);

            if (!RealmSyncPathHelper.TryValidateRealmFileAccessible(backupPath, out string? backupError))
                throw new IOException(backupError ?? "无法读取备份文件。");

            if (!RealmSyncPathHelper.TryValidateRealmFileAccessible(targetPath, out string? targetError))
                throw new IOException(targetError ?? "无法写入目标 Realm 文件（可能正被占用）。");

            if (string.Equals(backupPath, targetPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("备份路径与目标路径相同，拒绝还原。");

            if (!string.IsNullOrWhiteSpace(safetyBackupDirectory))
                CreateTimestampedCopy(targetPath, safetyBackupDirectory);

            File.Copy(backupPath, targetPath, overwrite: true);
        }
    }
}
