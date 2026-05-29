using NUnit.Framework;
using osu.Game.EzRealmSync.Mock;
using osu.Game.EzRealmSync.Realm;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class EzRealmSyncServiceFactoryTest
    {
        [Test]
        public void Create_ui_test_returns_mock()
        {
            Assert.That(EzRealmSyncServiceFactory.Create(uiTestMode: true), Is.InstanceOf<MockEzRealmSyncService>());
        }

        [Test]
        public void CreateDataService_without_lib_returns_stub()
        {
#if HAS_EZ_OSU_GAME
            Assert.That(EzRealmSyncServiceFactory.CreateDataService(uiTestMode: false), Is.InstanceOf<RealmRealmDataService>());
#else
            Assert.That(EzRealmSyncServiceFactory.CreateDataService(uiTestMode: false), Is.InstanceOf<StubRealmDataService>());
#endif
        }
    }
}
