using NUnit.Framework;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmApplySupportTest
    {
        [Test]
        public void SupportsDirection_includes_same_type()
        {
            Assert.That(RealmApplySupport.SupportsDirection(SyncDirection.EzToOfficial), Is.True);
            Assert.That(RealmApplySupport.SupportsDirection(SyncDirection.OfficialToEz), Is.True);
            Assert.That(RealmApplySupport.SupportsDirection(SyncDirection.EzToEz), Is.True);
            Assert.That(RealmApplySupport.SupportsDirection(SyncDirection.PpyToPpy), Is.True);
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
        public void ValidateApplyRequest_accepts_write_plan()
        {
            var request = new ApplyRequest
            {
                WritePlan = new RealmWritePlan
                {
                    SourceRealmFilePath = @"C:\a\data\client.realm",
                    TargetRealmFilePath = @"C:\b\data\client.realm",
                    SourceKind = RealmDiskSchemaKind.EzExtended,
                    TargetKind = RealmDiskSchemaKind.EzExtended,
                    LegacyDirection = SyncDirection.EzToEz,
                },
                ItemIds = new[] { Guid.NewGuid() },
            };
            Assert.That(RealmApplySupport.ValidateApplyRequest(request), Is.Null);
        }
    }
}
