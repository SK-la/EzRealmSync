#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Errors;

namespace osu.Game.EzRealmSync.Models
{
    /// <summary>
    /// EzRealmSync 打开 / 修复 Realm 时的 schema 边界：最低版见 <see cref="RealmSchemaRevisionCatalog"/>，最高版见 bundled lib。
    /// </summary>
    public static class RealmSchemaToolPolicy
    {
        /// <summary>bundled lib 官方 upstream。</summary>
        public static int MaxSupportedOfficialSchema => RealmAccess.UpstreamSchemaVersion;

        /// <summary>bundled lib Ez 文件 schema。</summary>
        public static int MaxSupportedEzFileSchema => RealmAccess.EzFileSchemaVersion;

        /// <summary>工具支持的最低官方磁盘 upstream（常量，非「同大版本」）。</summary>
        public static int MinSupportedOfficialSchema => RealmSchemaRevisionCatalog.MinSupportedOfficialUpstream;

        /// <summary>工具支持的最低 Ez 修订（常量）。</summary>
        public static int MinSupportedEzRevision => RealmSchemaRevisionCatalog.MinSupportedEzRevision;

        public static void EnsureCanOpen(int diskSchemaVersion)
        {
            if (diskSchemaVersion > MaxSupportedForKind(RealmSchemaSafety.Classify(diskSchemaVersion)))
            {
                throw new RealmUserOperationException(
                    RealmUserErrorKind.SchemaTooHigh,
                    $"这份 Realm 文件版本 {diskSchemaVersion} 高于本工具自带 dll 支持的 {LatestSupportedForKind(RealmSchemaSafety.Classify(diskSchemaVersion))}，请更新 EzRealmSync。");
            }

            if (!RealmSchemaRevisionCatalog.IsSupportedDiskSchema(diskSchemaVersion))
            {
                if (RealmSchemaSafety.IsOfficialDiskSchema(diskSchemaVersion))
                {
                    throw new RealmUserOperationException(
                        RealmUserErrorKind.SchemaTooLow,
                        $"这份官方 Realm 文件版本 {diskSchemaVersion} 低于本工具最低支持 {MinSupportedOfficialSchema}。请用对应版本客户端升级后再打开。");
                }

                var (official, ez) = RealmSchemaVersions.Decode(diskSchemaVersion);
                throw new RealmUserOperationException(
                    RealmUserErrorKind.SchemaTooLow,
                    $"这份 Ez Realm 文件版本 {diskSchemaVersion}（官方 {official}，Ez 修订 {ez}）低于本工具最低支持。请用对应版本客户端升级后再打开。");
            }
        }

        /// <summary>是否已在 lib 最新 schema（同类型）。</summary>
        public static bool IsAtLatestSupported(int diskSchemaVersion)
        {
            var kind = RealmSchemaSafety.Classify(diskSchemaVersion);
            return kind switch
            {
                RealmDiskSchemaKind.PpyClient => diskSchemaVersion == MaxSupportedOfficialSchema,
                RealmDiskSchemaKind.EzExtended => diskSchemaVersion == MaxSupportedEzFileSchema,
                _ => false,
            };
        }

        public static int LatestSupportedForKind(RealmDiskSchemaKind kind) =>
            kind == RealmDiskSchemaKind.PpyClient ? MaxSupportedOfficialSchema : MaxSupportedEzFileSchema;

        private static int MaxSupportedForKind(RealmDiskSchemaKind kind) =>
            LatestSupportedForKind(kind);
    }
}
#endif
