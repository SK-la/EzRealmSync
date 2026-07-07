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
        public void CreateDataService_real_mode_returns_realm_service()
        {
            Assert.That(EzRealmSyncServiceFactory.CreateDataService(uiTestMode: false), Is.InstanceOf<RealmRealmDataService>());
        }

        [Test]
        public void CreateSession_ui_test_uses_single_mock_instance()
        {
            var session = EzRealmSyncServiceFactory.CreateSession(uiTestMode: true);
            Assert.That(session.Data, Is.SameAs(session.Fix));
            Assert.That(session.Fix, Is.SameAs(session.Export));
            Assert.That(session.Sync, Is.SameAs(session.Data));
        }

        [Test]
        public void CreateSession_real_mode_shares_realm_data_service()
        {
            var session = EzRealmSyncServiceFactory.CreateSession(uiTestMode: false);
            Assert.That(session.Data, Is.SameAs(session.Fix));
            Assert.That(session.Fix, Is.SameAs(session.Export));
            Assert.That(session.Data, Is.InstanceOf<RealmRealmDataService>());
        }
    }
}
