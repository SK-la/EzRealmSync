namespace osu.Game.EzRealmSync.Models
{
    /// <summary>
    /// EzRealmSync 打开 Realm 时的策略：不迁移，仅接受工具可理解的磁盘版本。
    /// </summary>
    public static class RealmSchemaToolPolicy
    {
        /// <summary>当前 lib 中官方上游 schema（与 <see cref="osu.Game.Database.RealmAccess.UpstreamSchemaVersion"/> 对齐）。</summary>
        public static int OfficialUpstreamSchema => osu.Game.Database.RealmAccess.UpstreamSchemaVersion;

        /// <summary>当前 lib 中 Ez 端编码的最大 schema（official * 1000 + ez）。</summary>
        public static int MaxSupportedEzFileSchema => osu.Game.Database.RealmAccess.EzFileSchemaVersion;

        public static void EnsureCanOpen(int diskSchemaVersion)
        {
            if (RealmSchemaSafety.IsOfficialDiskSchema(diskSchemaVersion))
            {
                if (diskSchemaVersion > OfficialUpstreamSchema)
                    throw new InvalidOperationException($"官方库 schema {diskSchemaVersion} 高于本工具支持的 {OfficialUpstreamSchema}，请更新 EzRealmSync 或 lib。");

                return;
            }

            if (RealmSchemaSafety.IsEzClientDiskSchema(diskSchemaVersion))
            {
                if (diskSchemaVersion > MaxSupportedEzFileSchema)
                    throw new InvalidOperationException($"Ez 库 schema {diskSchemaVersion} 高于本工具支持的 {MaxSupportedEzFileSchema}，请更新 EzRealmSync 或 lib。");

                return;
            }

            throw new InvalidOperationException($"无法识别的 Realm schema 版本 {diskSchemaVersion}。");
        }
    }
}
