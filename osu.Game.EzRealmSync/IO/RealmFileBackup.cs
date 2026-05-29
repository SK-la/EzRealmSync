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
    }
}
