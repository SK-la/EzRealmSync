#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 探测磁盘 Realm 的 Schema 版本并选择正确的 <see cref="RealmAccess"/> 打开方式。
    /// </summary>
    public static class RealmSchemaProbe
    {
        public static int? TryReadSchemaVersion(string realmFilePath)
        {
            foreach (bool ez in new[] { true, false })
            {
                try
                {
                    using var access = ez
                        ? RealmDiffReader.OpenEzRealm(realmFilePath)
                        : RealmDiffReader.OpenOfficialRealm(realmFilePath);

                    int? version = null;
                    access.Run(realm => version = (int)realm.Config.SchemaVersion);
                    return version;
                }
                catch
                {
                    // 尝试另一种访问器
                }
            }

            return null;
        }

        public static RealmAccess Open(string realmFilePath, int? diskSchemaVersion = null)
        {
            int? schema = diskSchemaVersion ?? TryReadSchemaVersion(realmFilePath);

            if (RealmSchemaSafety.RequiresOfficialRealmAccess(schema))
                return RealmDiffReader.OpenOfficialRealm(realmFilePath);

            return RealmDiffReader.OpenEzRealm(realmFilePath);
        }
    }
}
#endif
