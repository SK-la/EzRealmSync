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
        public void Min_and_max_are_same_major()
        {
            Assert.That(RealmSchemaToolPolicy.MinSupportedOfficialSchema, Is.EqualTo(RealmAccess.UpstreamSchemaVersion));
            Assert.That(RealmSchemaToolPolicy.MaxSupportedOfficialSchema, Is.EqualTo(RealmAccess.UpstreamSchemaVersion));
            Assert.That(RealmSchemaToolPolicy.MinSupportedEzFileSchema, Is.EqualTo(RealmAccess.UpstreamSchemaVersion * 1000 + 1));
            Assert.That(RealmSchemaToolPolicy.MaxSupportedEzFileSchema, Is.EqualTo(RealmAccess.EzFileSchemaVersion));
            Assert.That(RealmSchemaToolPolicy.MaxSupportedEzFileSchema / 1000, Is.EqualTo(RealmAccess.UpstreamSchemaVersion));
        }

        [Test]
        public void EnsureCanOpen_rejects_below_min_ez()
        {
            int below = RealmSchemaToolPolicy.MinSupportedEzFileSchema - 1;
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
        public void EnsureCanOpen_accepts_current_ez()
        {
            Assert.DoesNotThrow((Action)(() => RealmSchemaToolPolicy.EnsureCanOpen(RealmAccess.EzFileSchemaVersion)));
        }
    }
}
#endif
