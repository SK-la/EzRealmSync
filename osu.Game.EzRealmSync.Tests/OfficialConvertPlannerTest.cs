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

        [Test]
        public void LibMinusOneUpstream_uses_lib_official_minus_one()
        {
            Assert.That(
                OfficialConvertPlanner.ResolveTargetOfficialUpstream(RealmAccess.EzFileSchemaVersion, OfficialConvertTarget.LibMinusOneUpstream),
                Is.EqualTo(RealmAccess.UpstreamSchemaVersion - 1));
        }

        [Test]
        public void ResolvePrimaryConvertTarget_at_lib_ez_returns_lib_minus_one()
        {
            Assert.That(
                OfficialConvertPlanner.ResolvePrimaryConvertTarget(RealmAccess.EzFileSchemaVersion),
                Is.EqualTo(OfficialConvertTarget.LibMinusOneUpstream));
        }

        [Test]
        public void ResolvePrimaryConvertTarget_legacy_ez_returns_preserve_read()
        {
            Assert.That(
                OfficialConvertPlanner.ResolvePrimaryConvertTarget(51_006),
                Is.EqualTo(OfficialConvertTarget.PreserveReadUpstream));
        }

        [Test]
        public void CanUseLibMinusOneConvert_false_for_legacy_ez()
        {
            Assert.That(OfficialConvertPlanner.CanUseLibMinusOneConvert(51_006), Is.False);
        }

        [Test]
        public void CanUseLibMinusOneConvert_true_when_lib_minus_one_supported()
        {
            if (RealmAccess.UpstreamSchemaVersion - 1 < RealmSchemaToolPolicy.MinSupportedOfficialSchema)
                Assert.Ignore("lib upstream 未高于最低官方号，跳过 lib-1 用例。");

            Assert.That(OfficialConvertPlanner.CanUseLibMinusOneConvert(RealmAccess.EzFileSchemaVersion), Is.True);
        }
    }
}
#endif
