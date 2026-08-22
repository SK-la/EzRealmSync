#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmSchemaToolPolicyTest
    {
        [Test]
        public void Min_is_constant_max_is_lib()
        {
            Assert.That(RealmSchemaToolPolicy.MinSupportedOfficialSchema, Is.EqualTo(RealmSchemaRevisionCatalog.MinSupportedOfficialUpstream));
            Assert.That(RealmSchemaToolPolicy.MaxSupportedOfficialSchema, Is.EqualTo(RealmAccess.UpstreamSchemaVersion));
            Assert.That(RealmSchemaToolPolicy.MaxSupportedEzFileSchema, Is.EqualTo(RealmAccess.EzFileSchemaVersion));
        }

        [Test]
        public void EnsureCanOpen_rejects_below_min_ez_revision()
        {
            int below = 51 * 1000 + (RealmSchemaRevisionCatalog.MinSupportedEzRevision - 1);
            var ex = Assert.Throws<RealmUserOperationException>((Action)(() => RealmSchemaToolPolicy.EnsureCanOpen(below)));
            Assert.That(ex!.Kind, Is.EqualTo(RealmUserErrorKind.SchemaTooLow));
        }

        [Test]
        public void EnsureCanOpen_rejects_above_max_ez()
        {
            int above = RealmSchemaToolPolicy.MaxSupportedEzFileSchema + 1;
            var ex = Assert.Throws<RealmUserOperationException>((Action)(() => RealmSchemaToolPolicy.EnsureCanOpen(above)));
            Assert.That(ex!.Kind, Is.EqualTo(RealmUserErrorKind.SchemaTooHigh));
        }

        [Test]
        public void EnsureCanOpen_accepts_51006_when_lib_is_newer()
        {
            if (RealmAccess.UpstreamSchemaVersion <= 51)
                Assert.Ignore("lib upstream 未高于 51，跳过 51006 用例。");

            Assert.DoesNotThrow((Action)(() => RealmSchemaToolPolicy.EnsureCanOpen(51_006)));
        }

        [Test]
        public void EnsureCanOpen_accepts_current_ez()
        {
            Assert.DoesNotThrow((Action)(() => RealmSchemaToolPolicy.EnsureCanOpen(RealmAccess.EzFileSchemaVersion)));
        }
    }
}
#endif
