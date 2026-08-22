#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Errors;

namespace osu.Game.EzRealmSync.Models
{
    public static class OfficialConvertPlanner
    {
        /// <summary>主按钮目标：lib 最新 Ez → lib-1；否则 → 读取号。</summary>
        public static OfficialConvertTarget ResolvePrimaryConvertTarget(int sourceDiskSchema)
        {
            if (RealmSchemaToolPolicy.IsAtLatestSupported(sourceDiskSchema))
                return OfficialConvertTarget.LibMinusOneUpstream;

            return OfficialConvertTarget.PreserveReadUpstream;
        }

        public static bool CanUseLibMinusOneConvert(int sourceDiskSchema)
        {
            if (!RealmSchemaToolPolicy.IsAtLatestSupported(sourceDiskSchema))
                return false;

            return RealmAccess.UpstreamSchemaVersion - 1 >= RealmSchemaToolPolicy.MinSupportedOfficialSchema;
        }

        public static int ResolveTargetOfficialUpstream(int sourceDiskSchema, OfficialConvertTarget convertTarget)
        {
            var (readOfficial, _) = RealmSchemaVersions.Decode(sourceDiskSchema);

            return convertTarget switch
            {
                OfficialConvertTarget.PreserveReadUpstream => readOfficial,
                OfficialConvertTarget.LibMinusOneUpstream => resolveLibMinusOneUpstream(),
                OfficialConvertTarget.UpgradeToLibUpstream => RealmAccess.UpstreamSchemaVersion,
                _ => throw new ArgumentOutOfRangeException(nameof(convertTarget), convertTarget, null),
            };
        }

        private static int resolveLibMinusOneUpstream()
        {
            int target = RealmAccess.UpstreamSchemaVersion - 1;

            if (target < RealmSchemaToolPolicy.MinSupportedOfficialSchema)
            {
                throw new RealmUserOperationException(
                    RealmUserErrorKind.SchemaTooLow,
                    $"lib-1 转官方目标 upstream {target} 低于本工具最低支持 {RealmSchemaToolPolicy.MinSupportedOfficialSchema}。");
            }

            return target;
        }
    }
}
#endif
