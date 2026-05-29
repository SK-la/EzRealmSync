using NUnit.Framework;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmApplySupportTest
    {
        [Test]
        public void SupportsDirection_only_EzToOfficial()
        {
            Assert.That(RealmApplySupport.SupportsDirection(SyncDirection.EzToOfficial), Is.True);
            Assert.That(RealmApplySupport.SupportsDirection(SyncDirection.OfficialToEz), Is.False);
        }

        [Test]
        public void ValidateApplyRequest_requires_items_and_paths()
        {
            var empty = new ApplyRequest
            {
                Direction = SyncDirection.EzToOfficial,
                Paths = new PathConfiguration { EzDataPath = @"C:\ez", OfficialDataPath = @"C:\off" },
            };
            Assert.That(RealmApplySupport.ValidateApplyRequest(empty), Is.Not.Null);

            var ok = new ApplyRequest
            {
                Direction = SyncDirection.EzToOfficial,
                Paths = new PathConfiguration { EzDataPath = @"C:\ez", OfficialDataPath = @"C:\off" },
                ItemIds = new[] { Guid.NewGuid() },
            };
            Assert.That(RealmApplySupport.ValidateApplyRequest(ok), Is.Null);
        }

        [Test]
        public void ValidateApplyRequest_rejects_official_to_ez()
        {
            var request = new ApplyRequest
            {
                Direction = SyncDirection.OfficialToEz,
                ItemIds = new[] { Guid.NewGuid() },
                Paths = new PathConfiguration { EzDataPath = @"C:\ez", OfficialDataPath = @"C:\off" },
            };
            Assert.That(RealmApplySupport.ValidateApplyRequest(request), Does.Contain("Ez→官方"));
        }
    }
}
