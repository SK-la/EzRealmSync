using osu.Game.EzRealmSync.Contracts;

namespace osu.Game.EzRealmSync
{
    public static class EzRealmSyncLog
    {
        private static readonly object sync = new object();
        private static bool initialized;
        private static string? logFilePath;

        public static void Initialize()
        {
            lock (sync)
            {
                if (initialized)
                    return;

                EzRealmSyncDataPaths.EnsureStandardDirectories();
                logFilePath = EzRealmSyncDataPaths.CurrentLogFilePath;
                initialized = true;
                writeLine("INFO", "EzRealmSync log initialized.");
                writeLine("INFO", $"ApplicationRoot={EzRealmSyncDataPaths.ApplicationRoot}");
            }
        }

        public static void Info(string message) => write("INFO", message);

        public static void Debug(string message) => write("DEBUG", message);

        public static void Warn(string message) => write("WARN", message);

        public static void Error(string message) => write("ERROR", message);

        public static void Exception(Exception ex, string? context = null)
        {
            string formatted = ExceptionFormatting.SafeFormat(ex);
            write("ERROR", context == null ? formatted : $"{context}{Environment.NewLine}{formatted}");
        }

        private static void write(string level, string message)
        {
            if (!initialized)
                Initialize();

            writeLine(level, message);
        }

        private static void writeLine(string level, string message)
        {
            lock (sync)
            {
                try
                {
                    string path = logFilePath ?? EzRealmSyncDataPaths.CurrentLogFilePath;
                    string line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
                    File.AppendAllText(path, line);
                }
                catch
                {
                    // 日志写入失败不得影响主流程。
                }
            }
        }
    }
}
