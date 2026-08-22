#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Errors;

namespace osu.Game.EzRealmSync.Models
{
    /// <summary>
    /// EzRealmSync 打开 Realm 时的策略：同大版本区间 [Min, Max]，不另存多版本 DLL。
    /// </summary>
    public static class RealmSchemaToolPolicy
    {
        /// <summary>当前 lib 官方上游 schema。</summary>
        public static int MaxSupportedOfficialSchema => RealmAccess.UpstreamSchemaVersion;

        /// <summary>当前 lib Ez 文件 schema（upstream * 1000 + ez）。</summary>
        public static int MaxSupportedEzFileSchema => RealmAccess.EzFileSchemaVersion;

        /// <summary>
        /// 本工具支持的最低官方磁盘 schema（同大版本 = 当前上游）。
        /// 工具未发布，可随时提高；低于此版本不提供旧 DLL。
        /// </summary>
        public static int MinSupportedOfficialSchema => MaxSupportedOfficialSchema;

        /// <summary>
        /// 本工具支持的最低 Ez 磁盘 schema（同上游大版本内最早 Ez 修订）。
        /// </summary>
        public static int MinSupportedEzFileSchema => MaxSupportedOfficialSchema * 1000 + 1;

        public static void EnsureCanOpen(int diskSchemaVersion)
        {
            if (RealmSchemaSafety.IsOfficialDiskSchema(diskSchemaVersion))
            {
                if (diskSchemaVersion > MaxSupportedOfficialSchema)
                {
                    throw new RealmUserOperationException(
                        RealmUserErrorKind.SchemaTooHigh,
                        $"官方库 schema {diskSchemaVersion} 高于本工具支持的 {MaxSupportedOfficialSchema}，请更新 EzRealmSync 或 lib。");
                }

                if (diskSchemaVersion < MinSupportedOfficialSchema)
                {
                    throw new RealmUserOperationException(
                        RealmUserErrorKind.SchemaTooLow,
                        $"官方库 schema {diskSchemaVersion} 低于本工具最低支持 {MinSupportedOfficialSchema}（当前仅支持同大版本）。请用对应版本客户端升到 {MinSupportedOfficialSchema} 及以上后再打开。");
                }

                return;
            }

            if (RealmSchemaSafety.IsEzClientDiskSchema(diskSchemaVersion))
            {
                if (diskSchemaVersion > MaxSupportedEzFileSchema)
                {
                    throw new RealmUserOperationException(
                        RealmUserErrorKind.SchemaTooHigh,
                        $"Ez 库 schema {diskSchemaVersion} 高于本工具支持的 {MaxSupportedEzFileSchema}，请更新 EzRealmSync 或 lib。");
                }

                if (diskSchemaVersion < MinSupportedEzFileSchema)
                {
                    throw new RealmUserOperationException(
                        RealmUserErrorKind.SchemaTooLow,
                        $"Ez 库 schema {diskSchemaVersion} 低于本工具最低支持 {MinSupportedEzFileSchema}（当前仅支持同大版本 {MaxSupportedOfficialSchema}xxx）。请用对应版本 Ez2Lazer 客户端升到 {MinSupportedEzFileSchema} 及以上后再打开。");
                }

                return;
            }

            throw new InvalidOperationException($"无法识别的 Realm schema 版本 {diskSchemaVersion}。");
        }

        /// <summary>是否已在工具当前最大 schema（同类型）。</summary>
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
