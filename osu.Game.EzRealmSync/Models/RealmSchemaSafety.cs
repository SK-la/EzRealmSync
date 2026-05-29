namespace osu.Game.EzRealmSync.Models
{
    /// <summary>
    /// 根据磁盘 <c>SchemaVersion</c> 判断库类型，避免用错访问器。
    /// </summary>
    public static class RealmSchemaSafety
    {
        /// <summary>官方 lazer 库：磁盘版本为裸 <c>51</c>（&lt; 1000）。</summary>
        public static bool IsOfficialDiskSchema(int? diskSchemaVersion) => diskSchemaVersion is > 0 and < 1000;

        /// <summary>Ez 客户端库：<c>official * 1000 + ez</c>（当前 51006）。</summary>
        public static bool IsEzClientDiskSchema(int? diskSchemaVersion) => diskSchemaVersion is >= 1000;

        public static bool RequiresOfficialRealmAccess(int? diskSchemaVersion) => IsOfficialDiskSchema(diskSchemaVersion);

        public static bool RequiresEzRealmAccess(int? diskSchemaVersion) => IsEzClientDiskSchema(diskSchemaVersion);

        public static RealmDiskSchemaKind Classify(int? diskSchemaVersion) => diskSchemaVersion switch
        {
            null => RealmDiskSchemaKind.Unknown,
            _ when IsEzClientDiskSchema(diskSchemaVersion) => RealmDiskSchemaKind.EzExtended,
            _ when IsOfficialDiskSchema(diskSchemaVersion) => RealmDiskSchemaKind.PpyClient,
            _ => RealmDiskSchemaKind.Unknown,
        };
    }
}
