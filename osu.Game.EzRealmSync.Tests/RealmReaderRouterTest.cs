#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.Realm.Readers;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmReaderRouterTest
    {
        private sealed class FakeAdapter(RealmReaderRoute route) : IRealmReaderAdapter
        {
            public RealmReaderRoute SupportedRoute => route;

            public RealmAccess Open(string realmFilePath, int pinnedDiskSchemaVersion) =>
                throw new InvalidOperationException($"route:{route}");
        }

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

        [Test]
        public void OpenByDiskSchemaVersion_uses_legacy_route_adapter()
        {
            int officialLegacy = Math.Max(1, RealmAccess.UpstreamSchemaVersion - 1);
            int ezLegacy = Math.Max(1000, RealmAccess.EzFileSchemaVersion - 1);

            var router = new RealmReaderRouter(
            [
                new FakeAdapter(RealmReaderRoute.OfficialCurrent),
                new FakeAdapter(RealmReaderRoute.OfficialLegacy),
                new FakeAdapter(RealmReaderRoute.EzCurrent),
                new FakeAdapter(RealmReaderRoute.EzLegacy),
            ]);

            var officialEx = Assert.Throws<InvalidOperationException>((Action)(() =>
                router.OpenByDiskSchemaVersion(officialLegacy, "official.realm")));
            Assert.That(officialEx!.Message, Does.Contain("route:OfficialLegacy"));

            var ezEx = Assert.Throws<InvalidOperationException>((Action)(() =>
                router.OpenByDiskSchemaVersion(ezLegacy, "ez.realm")));
            Assert.That(ezEx!.Message, Does.Contain("route:EzLegacy"));
        }

        [Test]
        public void OpenByDiskSchemaVersion_default_legacy_adapters_require_external_reader()
        {
            int officialLegacy = Math.Max(1, RealmAccess.UpstreamSchemaVersion - 1);
            int ezLegacy = Math.Max(1000, RealmAccess.EzFileSchemaVersion - 1);
            var router = new RealmReaderRouter();

            var officialEx = Assert.Throws<RealmUserOperationException>((Action)(() =>
                router.OpenByDiskSchemaVersion(officialLegacy, "official.realm")));
            Assert.That(officialEx!.Kind, Is.EqualTo(RealmUserErrorKind.LegacyReaderUnavailable));
            Assert.That(officialEx.Detail, Does.Contain("legacy official schema"));

            var ezEx = Assert.Throws<RealmUserOperationException>((Action)(() =>
                router.OpenByDiskSchemaVersion(ezLegacy, "ez.realm")));
            Assert.That(ezEx!.Kind, Is.EqualTo(RealmUserErrorKind.LegacyReaderUnavailable));
            Assert.That(ezEx.Detail, Does.Contain("legacy ez schema"));
        }
    }
}
#endif
