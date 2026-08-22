#if HAS_EZ_OSU_GAME
using osu.Game.Database;

namespace osu.Game.EzRealmSync.Models
{
    public static class OfficialConvertPlanner
    {
        public static int ResolveTargetOfficialUpstream(int sourceDiskSchema, OfficialConvertTarget convertTarget)
        {
            var (readOfficial, _) = RealmSchemaVersions.Decode(sourceDiskSchema);

            return convertTarget switch
            {
                OfficialConvertTarget.PreserveReadUpstream => readOfficial,
                OfficialConvertTarget.UpgradeToLibUpstream => RealmAccess.UpstreamSchemaVersion,
                _ => throw new ArgumentOutOfRangeException(nameof(convertTarget), convertTarget, null),
            };
        }
    }
}
#endif
