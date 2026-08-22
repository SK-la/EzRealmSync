using System.Text.Json;
using System.Text.Json.Serialization;
using osu.Game.EzRealmSync;

namespace osu.EzRealmSync.AppModel
{
    public sealed class EzRealmSyncAppSettings
    {
        /// <summary>导入页扫描路径：存储根目录下的 <c>*.realm</c>（及可选 <c>data/</c> 子目录）。</summary>
        public string SearchDirectory { get; set; } = string.Empty;

        public string? ImportSelectedRealmId { get; set; }

        public string? DataRealmId { get; set; }

        public string? SyncRealmIdA { get; set; }
        public string? SyncRealmIdB { get; set; }

        public string? FixRealmId { get; set; }

        public string? ExportRealmId { get; set; }

        public string BackupDirectory { get; set; } = string.Empty;

        public string ExportDirectory { get; set; } = string.Empty;

        public string ExportFolderName { get; set; } = string.Empty;

        /// <summary>批量导出成绩时按玩家名创建子文件夹。</summary>
        public bool ExportGroupScoresByPlayer { get; set; } = true;

        public string IllegalCharacterReplacement { get; set; } = "_";

        /// <summary>删除表格行前是否弹出确认对话框。</summary>
        public bool ConfirmBeforeDelete { get; set; } = true;

        /// <summary>UI 测试模式（Mock 数据）；可在设置中切换，无需重启。</summary>
        public bool UiTestMode { get; set; }

        /// <summary>可选：覆盖 reader 包扫描目录（默认 exe/readers）。</summary>
        public string ReaderPackagesDirectory { get; set; } = string.Empty;

        /// <summary>启动时使用的 reader 包 ID；留空表示使用内置 NuGet/本地 lib。</summary>
        public string? ActiveReaderPackageId { get; set; }
    }

    public static class AppSettingsStore
    {
        private static readonly JsonSerializerOptions json_options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public static EzRealmSyncAppSettings Load(string? settingsPath = null)
        {
            string path = settingsPath ?? EzRealmSyncDataPaths.ResolveSettingsFile();

            try
            {
                if (!File.Exists(path))
                    return createDefault();

                string json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<EzRealmSyncAppSettings>(json, json_options) ?? createDefault();
                applyDefaults(settings);
                return settings;
            }
            catch
            {
                return createDefault();
            }
        }

        public static void Save(EzRealmSyncAppSettings settings, string? settingsPath = null)
        {
            string path = settingsPath ?? EzRealmSyncDataPaths.ResolveSettingsWriteFile();
            string? dir = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(settings, json_options);
            File.WriteAllText(path, json);
        }

        private static EzRealmSyncAppSettings createDefault()
        {
            EzRealmSyncDataPaths.EnsureStandardDirectories();

            return new EzRealmSyncAppSettings
            {
                BackupDirectory = EzRealmSyncDataPaths.BackupsDirectory,
                ExportDirectory = EzRealmSyncDataPaths.ExportsDirectory,
                IllegalCharacterReplacement = "_",
            };
        }

        private static void applyDefaults(EzRealmSyncAppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.BackupDirectory))
                settings.BackupDirectory = EzRealmSyncDataPaths.BackupsDirectory;

            if (string.IsNullOrWhiteSpace(settings.ExportDirectory))
                settings.ExportDirectory = EzRealmSyncDataPaths.ExportsDirectory;

            if (string.IsNullOrWhiteSpace(settings.ReaderPackagesDirectory))
                settings.ReaderPackagesDirectory = EzRealmSyncDataPaths.ReadersDirectory;
        }
    }
}
