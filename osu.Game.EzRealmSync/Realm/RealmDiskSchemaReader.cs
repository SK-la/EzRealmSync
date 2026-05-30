#if HAS_EZ_OSU_GAME
using RealmConfiguration = Realms.RealmConfiguration;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 只读探测磁盘 schema 版本（动态 Realm 打开，不经过 <see cref="osu.Game.Database.RealmAccess"/>，不迁移）。
    /// </summary>
    internal static class RealmDiskSchemaReader
    {
        public static int? TryReadSchemaVersion(string realmFilePath)
        {
            string fullPath = Path.GetFullPath(realmFilePath);

            if (!File.Exists(fullPath))
                return null;

            return tryReadDynamic(fullPath, out int? dynamicVersion) && isRecognisedVersion(dynamicVersion)
                ? dynamicVersion
                : null;
        }

        private static bool tryReadDynamic(string fullPath, out int? schemaVersion)
        {
            schemaVersion = null;

            try
            {
                string tempPathLocation = Path.Combine(Path.GetTempPath(), @"lazer");
                if (!Directory.Exists(tempPathLocation))
                    Directory.CreateDirectory(tempPathLocation);

                var config = new RealmConfiguration(fullPath)
                {
                    IsDynamic = true,
                    FallbackPipePath = tempPathLocation,
                };

                using var realm = RealmInstance.GetInstance(config);
                ulong version = realm.Config.SchemaVersion;

                if (version == 0)
                    return false;

                schemaVersion = checked((int)version);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool isRecognisedVersion(int? version) =>
            version is > 0 and (< 1000 or >= 1000);
    }
}
#endif
