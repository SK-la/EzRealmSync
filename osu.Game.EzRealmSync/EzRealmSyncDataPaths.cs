namespace osu.Game.EzRealmSync
{
    /// <summary>
    /// EzRealmSync 运行时数据目录：settings、readers、备份、导出、临时文件。
    /// Worker 子目录（read-sidecar 等）会解析到 host exe 根目录。
    /// </summary>
    public static class EzRealmSyncDataPaths
    {
        private static readonly string[] worker_subdirectories = { "read-sidecar", "official-write" };

        /// <summary>Host 应用根目录（EzRealmSync.exe 所在目录；Worker 进程为上级目录）。</summary>
        public static string ResolveHostApplicationRoot()
        {
            string baseDir = Path.GetFullPath(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string leaf = Path.GetFileName(baseDir);

            if (worker_subdirectories.Any(name => string.Equals(leaf, name, StringComparison.OrdinalIgnoreCase)))
                return Path.GetFullPath(Path.Combine(baseDir, ".."));

            return baseDir;
        }

        /// <summary>用户 AppData 配置目录。</summary>
        public static string AppDataDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EzRealmSync");

        /// <summary>exe 旁 settings.json。</summary>
        public static string HostSettingsFile => Path.Combine(ResolveHostApplicationRoot(), "settings.json");

        /// <summary>AppData settings.json。</summary>
        public static string AppDataSettingsFile => Path.Combine(AppDataDirectory, "settings.json");

        /// <summary>优先 exe 旁 settings.json，不存在则 AppData，均不存在则默认写 exe 旁。</summary>
        public static string ResolveSettingsFile()
        {
            if (File.Exists(HostSettingsFile))
                return HostSettingsFile;

            if (File.Exists(AppDataSettingsFile))
                return AppDataSettingsFile;

            return HostSettingsFile;
        }

        /// <summary>解析 settings 写入路径（已有文件优先，否则 exe 旁）。</summary>
        public static string ResolveSettingsWriteFile() => ResolveSettingsFile();

        public static string ApplicationRoot => ResolveHostApplicationRoot();

        public static string SettingsFile => ResolveSettingsFile();

        public static string ReadersDirectory => Path.Combine(ApplicationRoot, "readers");

        public static string BackupsDirectory => Path.Combine(ApplicationRoot, "backups");

        public static string ExportsDirectory => Path.Combine(ApplicationRoot, "exports");

        public static string TempDirectory => Path.Combine(ApplicationRoot, "temp");

        public static string LogsDirectory => Path.Combine(ApplicationRoot, "log");

        public static string CurrentLogFilePath => Path.Combine(LogsDirectory, $"EzRealmSync_{DateTime.Now:yyyyMMdd}.log");

        public static string RealmPipeDirectory => Path.Combine(TempDirectory, "lazer");

        public static string DefaultRuntimeLibDirectory => ApplicationRoot;

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

        /// <summary>将 settings 中的路径解析为绝对路径（相对 host 根目录）。</summary>
        public static string? ResolveConfiguredPath(string? configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
                return null;

            return Path.IsPathRooted(configuredPath)
                ? Path.GetFullPath(configuredPath)
                : Path.GetFullPath(Path.Combine(ApplicationRoot, configuredPath));
        }
    }
}
