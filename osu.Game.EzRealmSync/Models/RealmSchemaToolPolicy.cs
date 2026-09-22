#if HAS_EZ_OSU_GAME
using osu.Game.Database;

namespace osu.Game.EzRealmSync.Models
{
    /// <summary>
    /// EzRealmSync 的 schema 识别口径：bundled lib 的版本号与最低支持版本，仅用于识别/展示与「是否已是最新」判断，
    /// <b>不</b>再作为能否读写某份库的开关（动态打开不吃版本号）。
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
    }
}
#endif
