namespace osu.Game.EzRealmSync
{
    /// <summary>
    /// EzRealmSync 运行时数据目录：settings、readers、备份、导出、临时文件均位于 exe 同目录。
    /// </summary>
    public static class EzRealmSyncDataPaths
    {
        public static string ApplicationRoot => Path.GetFullPath(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        public static string SettingsFile => Path.Combine(ApplicationRoot, "settings.json");

        public static string ReadersDirectory => Path.Combine(ApplicationRoot, "readers");

        public static string BackupsDirectory => Path.Combine(ApplicationRoot, "backups");

        public static string ExportsDirectory => Path.Combine(ApplicationRoot, "exports");

        public static string TempDirectory => Path.Combine(ApplicationRoot, "temp");

        public static string LogsDirectory => Path.Combine(ApplicationRoot, "log");

        public static string CurrentLogFilePath => Path.Combine(LogsDirectory, $"EzRealmSync_{DateTime.Now:yyyyMMdd}.log");

        public static string RealmPipeDirectory => Path.Combine(TempDirectory, "lazer");

        public static void EnsureStandardDirectories()
        {
            Directory.CreateDirectory(ReadersDirectory);
            Directory.CreateDirectory(BackupsDirectory);
            Directory.CreateDirectory(ExportsDirectory);
            Directory.CreateDirectory(TempDirectory);
            Directory.CreateDirectory(LogsDirectory);
        }

        public static string CreateTempSubdirectory(string category)
        {
            string root = Path.Combine(TempDirectory, category, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }
    }
}
