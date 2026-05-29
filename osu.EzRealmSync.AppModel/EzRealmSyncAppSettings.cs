using System.Text.Json;
using System.Text.Json.Serialization;

namespace osu.EzRealmSync.AppModel
{
    public sealed class EzRealmSyncAppSettings
    {
        public string SearchDirectory { get; set; } = string.Empty;

        public string BackupDirectory { get; set; } = string.Empty;

        public string ExportDirectory { get; set; } = string.Empty;

        public string ExportFolderName { get; set; } = string.Empty;

        public string IllegalCharacterReplacement { get; set; } = "_";
    }

    public static class AppSettingsStore
    {
        private static readonly JsonSerializerOptions jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private static string settingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EzRealmSync",
            "settings.json");

        public static EzRealmSyncAppSettings Load()
        {
            try
            {
                if (!File.Exists(settingsPath))
                    return createDefault();

                string json = File.ReadAllText(settingsPath);
                return JsonSerializer.Deserialize<EzRealmSyncAppSettings>(json, jsonOptions) ?? createDefault();
            }
            catch
            {
                return createDefault();
            }
        }

        public static void Save(EzRealmSyncAppSettings settings)
        {
            string? dir = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(settings, jsonOptions);
            File.WriteAllText(settingsPath, json);
        }

        private static EzRealmSyncAppSettings createDefault() => new()
        {
            BackupDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EzRealmSync", "backups"),
            ExportDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EzRealmSync", "exports"),
            IllegalCharacterReplacement = "_",
        };
    }
}
