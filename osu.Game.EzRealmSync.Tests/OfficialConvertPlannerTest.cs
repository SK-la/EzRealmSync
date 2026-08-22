#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class OfficialConvertPlannerTest
    {
        [Test]
        public void PreserveReadUpstream_decodes_ez_disk_schema()
        {
            Assert.That(
                OfficialConvertPlanner.ResolveTargetOfficialUpstream(51_006, OfficialConvertTarget.PreserveReadUpstream),
                Is.EqualTo(51));
        }

        [Test]
        public void UpgradeToLibUpstream_uses_lib_official_schema()
        {
            Assert.That(
                OfficialConvertPlanner.ResolveTargetOfficialUpstream(51_006, OfficialConvertTarget.UpgradeToLibUpstream),
                Is.EqualTo(RealmAccess.UpstreamSchemaVersion));
        }
    }
}
#endif
