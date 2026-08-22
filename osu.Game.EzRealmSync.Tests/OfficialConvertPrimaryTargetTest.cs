#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class OfficialConvertPrimaryTargetTest
    {
        [Test]
        public void Primary_button_label_target_matches_planner_for_legacy_and_lib()
        {
            int legacy = 51_006;
            int libEz = osu.Game.Database.RealmAccess.EzFileSchemaVersion;

            Assert.That(OfficialConvertPlanner.ResolvePrimaryConvertTarget(legacy), Is.EqualTo(OfficialConvertTarget.PreserveReadUpstream));
            Assert.That(OfficialConvertPlanner.ResolvePrimaryConvertTarget(libEz), Is.EqualTo(OfficialConvertTarget.LibMinusOneUpstream));
        }
    }
}
#endif
