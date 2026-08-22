#if HAS_EZ_OSU_GAME
using System.Reflection;
using System.Text.RegularExpressions;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;
using RealmConfiguration = Realms.RealmConfiguration;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 只读探测磁盘 schema 版本（动态 Realm 打开，不经过 <see cref="osu.Game.Database.RealmAccess"/>，不迁移）。
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
                // 禁止回退到 Config.SchemaVersion：在 Ez 程序集内打开时它可能反映当前模型版本（如 51006）而非磁盘版本。
                ulong version = readSchemaVersionFromHandle(realm);

                if (version == 0)
                {
                    error = "已打开文件但无法从 native handle 读取 schema 版本（请确认 realm-wrappers 完整且游戏已关闭）。";
                    return false;
                }

                schemaVersion = normalizeProbedVersion(fullPath, checked((int)version));
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

        /// <summary>
        /// 裸 <c>client.realm</c> 在官方目录中应为 upstream-only（&lt; 1000）。
        /// 若动态探测误报 Ez 编码（如 51006），用 OfficialRealmAccess 只读打开校验。
        /// </summary>
        private static int normalizeProbedVersion(string fullPath, int probedVersion)
        {
            if (!isPlainClientRealm(Path.GetFileName(fullPath)))
                return probedVersion;

            if (probedVersion < 1000)
                return probedVersion;

            int upstream = RealmAccess.UpstreamSchemaVersion;
            if (tryOfficialOpenWithoutMigration(fullPath, upstream))
                return upstream;

            var (official, _) = RealmSchemaVersions.Decode(probedVersion);
            if (official > 0 && official != upstream && tryOfficialOpenWithoutMigration(fullPath, official))
                return official;

            return probedVersion;
        }

        private static bool isPlainClientRealm(string fileName) =>
            fileName.Equals("client.realm", StringComparison.OrdinalIgnoreCase);

        private static bool tryOfficialOpenWithoutMigration(string fullPath, int pinnedSchema)
        {
            try
            {
                string storageRoot = RealmWorkspacePaths.ResolveStorageRoot(fullPath);
                string filename = Path.GetFileName(fullPath);
                using var access = OfficialRealmAccess.OpenWithoutMigration(new NativeStorage(storageRoot), filename, pinnedSchema);
                access.Run(_ => { });
                return true;
            }
            catch
            {
                return false;
            }
        }

        [GeneratedRegex(@"^client_(?<n>\d+)\.realm$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant)]
        private static partial Regex myRegex();
    }
}
#endif
