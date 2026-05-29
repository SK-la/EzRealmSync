namespace osu.Game.EzRealmSync.Models
{
    /// <summary>
    /// Realm 文件头版本编码：official * 1000 + ez（例 51003 = 官方 51 + Ez 3）。
    /// </summary>
    public static class RealmSchemaVersions
    {
        public static int Encode(int officialSchemaVersion, int ezRealmSchemaVersion) =>
            officialSchemaVersion * 1000 + ezRealmSchemaVersion;

        public static (int official, int ez) Decode(int? combinedVersion)
        {
            if (!combinedVersion.HasValue)
                return (0, 0);

            int value = combinedVersion.Value;
            return (value / 1000, value % 1000);
        }
    }
}
