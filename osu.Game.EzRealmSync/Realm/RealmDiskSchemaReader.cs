#if HAS_EZ_OSU_GAME
using System.Reflection;
using System.Text.RegularExpressions;
using RealmConfiguration = Realms.RealmConfiguration;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 只读探测磁盘 schema 版本（动态 Realm 打开，不经过 <see cref="osu.Game.Database.RealmAccess"/>，不迁移）。
    /// </summary>
    public static class RealmDiskSchemaReader
    {
        private static readonly Regex versioned_filename = new(
            @"^client_(?<n>\d+)\.realm$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

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

            // client_51006.realm → 51006；client_51.realm → 51（不含 client_51_51 这类双后缀）
            schemaVersion = value;
            return isRecognisedVersion(schemaVersion);
        }

        private static bool tryReadDynamic(string fullPath, out int? schemaVersion, out string? error)
        {
            schemaVersion = null;
            error = null;

            try
            {
                string tempPathLocation = Path.Combine(Path.GetTempPath(), @"lazer");
                if (!Directory.Exists(tempPathLocation))
                    Directory.CreateDirectory(tempPathLocation);

                var config = new RealmConfiguration(fullPath)
                {
                    IsDynamic = true,
                    IsReadOnly = true,
                    FallbackPipePath = tempPathLocation,
                };

                using var realm = RealmInstance.GetInstance(config);

                // Realm 20：动态只读打开后 Config.SchemaVersion 仍为 0，须从 native handle 读取。
                ulong version = readSchemaVersionFromHandle(realm);

                if (version == 0)
                    version = realm.Config.SchemaVersion;

                if (version == 0)
                {
                    error = "已打开文件但 schema 版本为 0（请确认 lib/runtimes/win-x64/native/realm-wrappers.dll 存在且游戏已关闭）。";
                    return false;
                }

                schemaVersion = checked((int)version);
                return true;
            }
            catch (Exception ex)
            {
                error = formatOpenFailure(ex);
                return false;
            }
        }

        private static ulong readSchemaVersionFromHandle(RealmInstance realm)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            object? handle = typeof(RealmInstance).GetField("SharedRealmHandle", flags)?.GetValue(realm);
            if (handle == null)
                return 0;

            MethodInfo? method = handle.GetType().GetMethod("GetSchemaVersion", flags, binder: null, Type.EmptyTypes, modifiers: null);
            if (method == null)
                return 0;

            return (ulong)method.Invoke(handle, null)!;
        }

        private static string formatOpenFailure(Exception ex)
        {
            string message = ex.InnerException?.Message ?? ex.Message;

            if (message.Contains("realm-wrappers", StringComparison.OrdinalIgnoreCase)
                || message.Contains("DllNotFound", StringComparison.OrdinalIgnoreCase)
                || message.Contains("unable to load", StringComparison.OrdinalIgnoreCase))
            {
                return "无法加载 Realm 原生库 realm-wrappers.dll。请执行 dotnet build -t:SyncEzRealmLibs 并确认 exe/lib/runtimes/win-x64/native/ 完整。";
            }

            if (message.Contains("lock", StringComparison.OrdinalIgnoreCase)
                || message.Contains("in use", StringComparison.OrdinalIgnoreCase)
                || message.Contains("正在使用", StringComparison.OrdinalIgnoreCase))
            {
                return "Realm 文件正被占用，请先关闭 osu!/Ez2Lazer。";
            }

            return message;
        }

        private static bool isRecognisedVersion(int? version) =>
            version is > 0 and (< 1000 or >= 1000);
    }
}
#endif
