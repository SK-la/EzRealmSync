#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.Realm.Readers;
using Realms;
using RealmInstance = Realms.Realm;

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
        public void OpenByDiskSchemaVersion_legacy_falls_back_to_reader_guidance_when_pinned_open_fails()
        {
            int officialLegacy = Math.Max(1, RealmAccess.UpstreamSchemaVersion - 1);
            int ezLegacy = Math.Max(1000, RealmAccess.EzFileSchemaVersion - 1);
            var router = new RealmReaderRouter();

            string officialPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"missing-official-{Guid.NewGuid():N}.realm");
            string ezPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"missing-ez-{Guid.NewGuid():N}.realm");
            File.WriteAllBytes(officialPath, [0x01, 0x02, 0x03]);
            File.WriteAllBytes(ezPath, [0x01, 0x02, 0x03]);

            try
            {
                var officialEx = Assert.Throws<RealmUserOperationException>((Action)(() =>
                    router.OpenByDiskSchemaVersion(officialLegacy, officialPath)));
                Assert.That(officialEx!.Kind, Is.EqualTo(RealmUserErrorKind.LegacyReaderUnavailable));

                var ezEx = Assert.Throws<RealmUserOperationException>((Action)(() =>
                    router.OpenByDiskSchemaVersion(ezLegacy, ezPath)));
                Assert.That(ezEx!.Kind, Is.EqualTo(RealmUserErrorKind.LegacyReaderUnavailable));
            }
            finally
            {
                foreach (string path in new[] { officialPath, ezPath })
                {
                    if (File.Exists(path))
                        File.Delete(path);

                    string lockPath = path + ".lock";
                    if (File.Exists(lockPath))
                        File.Delete(lockPath);
                }
            }
        }

        [Test]
        public void OpenByDiskSchemaVersion_legacy_ez_schema_tries_pinned_open()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"legacy_ez_{Guid.NewGuid():N}.realm");

            try
            {
                var writeConfig = new RealmConfiguration(path) { SchemaVersion = 51_003 };
                using (RealmInstance.GetInstance(writeConfig))
                {
                }

                var router = new RealmReaderRouter();
                Assert.DoesNotThrow((Action)(() =>
                {
                    using var access = router.OpenByDiskSchemaVersion(51_003, path);
                    access.Run(_ => { });
                }));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);

                string lockPath = path + ".lock";
                if (File.Exists(lockPath))
                    File.Delete(lockPath);
            }
        }

        [Test]
        public void OpenBySchemaKind_uses_same_route_as_disk_version()
        {
            int officialLegacy = Math.Max(1, RealmAccess.UpstreamSchemaVersion - 1);
            var router = new RealmReaderRouter(
            [
                new FakeAdapter(RealmReaderRoute.OfficialCurrent),
                new FakeAdapter(RealmReaderRoute.OfficialLegacy),
                new FakeAdapter(RealmReaderRoute.EzCurrent),
                new FakeAdapter(RealmReaderRoute.EzLegacy),
            ]);

            var ex = Assert.Throws<InvalidOperationException>((Action)(() =>
                router.OpenBySchemaKind(Models.RealmDiskSchemaKind.PpyClient, "official.realm", officialLegacy)));
            Assert.That(ex!.Message, Does.Contain("route:OfficialLegacy"));
        }
    }
}
#endif
