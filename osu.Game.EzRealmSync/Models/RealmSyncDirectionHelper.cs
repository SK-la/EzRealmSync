namespace osu.Game.EzRealmSync.Models
{
    /// <summary>
    /// 根据已注册 Realm 的 schema 推断 Ez↔官方 同步方向与 <see cref="PathConfiguration"/>。
    /// </summary>
    public static class RealmSyncDirectionHelper
    {
        public static bool TryInferDirection(RealmFileEntry source, RealmFileEntry target, out SyncDirection direction, out string? error)
        {
            direction = default;
            error = null;

            bool sourceEz = RealmSchemaSafety.IsEzClientDiskSchema(source.SchemaVersion);
            bool sourceOfficial = RealmSchemaSafety.IsOfficialDiskSchema(source.SchemaVersion);
            bool targetEz = RealmSchemaSafety.IsEzClientDiskSchema(target.SchemaVersion);
            bool targetOfficial = RealmSchemaSafety.IsOfficialDiskSchema(target.SchemaVersion);

            if (sourceEz && targetOfficial)
            {
                direction = SyncDirection.EzToOfficial;
                return true;
            }

            if (sourceOfficial && targetEz)
            {
                direction = SyncDirection.OfficialToEz;
                return true;
            }

            error = "源与目标须为「Ez 客户端库 + 官方 lazer 库」组合，且 schema 版本已识别。";
            return false;
        }

        public static PathConfiguration CreatePaths(RealmFileEntry source, RealmFileEntry target, SyncDirection direction)
        {
            RealmFileEntry ezFile = direction == SyncDirection.EzToOfficial ? source : target;
            RealmFileEntry officialFile = direction == SyncDirection.EzToOfficial ? target : source;

            return new PathConfiguration
            {
                EzDataPath = dataDirectory(ezFile),
                OfficialDataPath = dataDirectory(officialFile),
            };
        }

        private static string dataDirectory(RealmFileEntry file) =>
            file.DataDirectory ?? Path.GetDirectoryName(file.FilePath) ?? string.Empty;
    }
}
