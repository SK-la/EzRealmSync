using System.Text.RegularExpressions;
using RealmConfiguration = Realms.RealmConfiguration;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 只读探测磁盘 schema 版本（动态 Realm 打开，不经过 osu.Game 模型，不迁移）。
    /// </summary>
    public static partial class RealmDiskSchemaReader
    {
        private static readonly Regex versioned_filename = myRegex();

        public static int? TryReadSchemaVersion(string realmFilePath) =>
            TryReadSchemaVersion(realmFilePath, out _);

        public static int? TryReadSchemaVersion(string realmFilePath, out string? error)
        {
            error = null;
            string fullPath = Path.GetFullPath(realmFilePath);

            if (!File.Exists(fullPath))
            {
                error = "文件不存在。";
                return null;
            }

            if (tryReadDynamic(fullPath, out int? dynamicVersion, out string? dynamicError))
                return dynamicVersion;

            if (tryInferFromFilename(Path.GetFileName(fullPath), out int? fromName))
                return fromName;

            error = dynamicError ?? "动态只读打开失败。";
            return null;
        }

        private static bool tryInferFromFilename(string fileName, out int? schemaVersion)
        {
            schemaVersion = null;
            var match = versioned_filename.Match(fileName);

            if (!match.Success || !int.TryParse(match.Groups["n"].Value, out int value))
                return false;

            schemaVersion = value;
            return value > 0;
        }

        private static bool tryReadDynamic(string fullPath, out int? schemaVersion, out string? error)
        {
            schemaVersion = null;
            error = null;

            try
            {
                string tempPathLocation = EzRealmSyncDataPaths.RealmPipeDirectory;
                Directory.CreateDirectory(tempPathLocation);

                var config = new RealmConfiguration(fullPath)
                {
                    IsDynamic = true,
                    IsReadOnly = true,
                    Schema = Array.Empty<Type>(),
                    FallbackPipePath = tempPathLocation,
                };

                using var realm = RealmOpenContext.GetInstance(config);
                ulong version = DynamicRealmSession.ReadSchemaVersionFromHandle(realm);

                if (version == 0 || version > int.MaxValue)
                {
                    error = "已打开文件但无法从 native handle 读取 schema 版本（请确认 realm-wrappers 完整且游戏已关闭）。";
                    return false;
                }

                schemaVersion = (int)version;
                return true;
            }
            catch (Exception ex)
            {
                error = formatOpenFailure(ex);
                return false;
            }
        }

        private static string formatOpenFailure(Exception ex)
        {
            string message = ex.InnerException?.Message ?? ex.Message;

            if (message.Contains("realm-wrappers", StringComparison.OrdinalIgnoreCase)
                || message.Contains("DllNotFound", StringComparison.OrdinalIgnoreCase)
                || message.Contains("unable to load", StringComparison.OrdinalIgnoreCase))
            {
                return "无法加载 Realm 原生库 realm-wrappers.dll。";
            }

            if (message.Contains("lock", StringComparison.OrdinalIgnoreCase)
                || message.Contains("in use", StringComparison.OrdinalIgnoreCase)
                || message.Contains("正在使用", StringComparison.OrdinalIgnoreCase))
            {
                return "Realm 文件正被占用，请先关闭 osu!/Ez2Lazer。";
            }

            return message;
        }

        [GeneratedRegex(@"^client_(?<n>\d+)\.realm$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant)]
        private static partial Regex myRegex();
    }
}
