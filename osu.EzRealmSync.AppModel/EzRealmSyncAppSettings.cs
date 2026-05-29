using System.Text.Json;
using System.Text.Json.Serialization;

namespace osu.EzRealmSync.AppModel
{
    public sealed class EzRealmSyncAppSettings
    {
        /// <summary>导入页扫描路径：存储根目录下的 <c>*.realm</c>（及可选 <c>data/</c> 子目录）。</summary>
        public string SearchDirectory { get; set; } = string.Empty;

        /// <summary>兼容旧版 settings.json；读取后迁移到 <see cref="SearchDirectory"/>，不再写入。</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string EndpointAWorkspace { get; set; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string EndpointBWorkspace { get; set; } = string.Empty;

        public string? ImportSelectedRealmId { get; set; }

        public string? DataRealmId { get; set; }

        public string? SyncRealmIdA { get; set; }
        public string? SyncRealmIdB { get; set; }

        public string? FixRealmId { get; set; }

        public string? ExportRealmId { get; set; }

        public string BackupDirectory { get; set; } = string.Empty;

        public string ExportDirectory { get; set; } = string.Empty;

        public string ExportFolderName { get; set; } = string.Empty;

        public string IllegalCharacterReplacement { get; set; } = "_";

        /// <summary>删除表格行前是否弹出确认对话框。</summary>
        public bool ConfirmBeforeDelete { get; set; } = true;
    }

    public static class AppSettingsStore
    {
        private static readonly JsonSerializerOptions jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public static string GetDefaultSettingsPath() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EzRealmSync",
            "settings.json");

        public static EzRealmSyncAppSettings Load(string? settingsPath = null)
        {
            string path = settingsPath ?? GetDefaultSettingsPath();

            try
            {
                if (!File.Exists(path))
                    return createDefault();

                string json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<EzRealmSyncAppSettings>(json, jsonOptions) ?? createDefault();
                migrateLegacyPaths(settings);
                return settings;
            }
            catch
            {
                return createDefault();
            }
        }

        public static void Save(EzRealmSyncAppSettings settings, string? settingsPath = null)
        {
            string path = settingsPath ?? GetDefaultSettingsPath();
            string? dir = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(settings, jsonOptions);
            File.WriteAllText(path, json);
        }

        private static EzRealmSyncAppSettings createDefault() => new()
        {
            BackupDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EzRealmSync", "backups"),
            ExportDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EzRealmSync", "exports"),
            IllegalCharacterReplacement = "_",
        };

        private static void migrateLegacyPaths(EzRealmSyncAppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.SearchDirectory))
            {
                if (!string.IsNullOrWhiteSpace(settings.EndpointAWorkspace))
                    settings.SearchDirectory = settings.EndpointAWorkspace;
                else if (!string.IsNullOrWhiteSpace(settings.EndpointBWorkspace))
                    settings.SearchDirectory = settings.EndpointBWorkspace;
            }
        }
    }
}
