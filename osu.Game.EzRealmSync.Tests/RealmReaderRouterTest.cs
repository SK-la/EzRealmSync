#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.EzRealmSync.Realm.Readers;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmReaderRouterTest
    {
        [Test]
        public void ResolveRoute_classifies_current_and_legacy_versions()
        {
            var router = new RealmReaderRouter();

            Assert.That(router.ResolveRoute(RealmAccess.UpstreamSchemaVersion), Is.EqualTo(RealmReaderRoute.OfficialCurrent));
            Assert.That(router.ResolveRoute(RealmAccess.EzFileSchemaVersion), Is.EqualTo(RealmReaderRoute.EzCurrent));

            int officialLegacy = Math.Max(1, RealmAccess.UpstreamSchemaVersion - 1);
            Assert.That(router.ResolveRoute(officialLegacy), Is.EqualTo(RealmReaderRoute.OfficialLegacy));

            int ezLegacy = Math.Max(1000, RealmAccess.EzFileSchemaVersion - 1);
            Assert.That(router.ResolveRoute(ezLegacy), Is.EqualTo(RealmReaderRoute.EzLegacy));
        }
    }
}
#endif
