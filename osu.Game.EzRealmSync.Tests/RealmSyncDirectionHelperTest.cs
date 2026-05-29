using NUnit.Framework;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmSyncDirectionHelperTest
    {
        [Test]
        public void TryInferDirection_ez_to_official()
        {
            var ez = entry(schema: 51_006);
            var official = entry(schema: 51);

            Assert.That(RealmSyncDirectionHelper.TryInferDirection(ez, official, out var direction, out _), Is.True);
            Assert.That(direction, Is.EqualTo(SyncDirection.EzToOfficial));
        }

        [Test]
        public void TryInferDirection_official_to_ez()
        {
            var official = entry(schema: 51);
            var ez = entry(schema: 51_006);

            Assert.That(RealmSyncDirectionHelper.TryInferDirection(official, ez, out var direction, out _), Is.True);
            Assert.That(direction, Is.EqualTo(SyncDirection.OfficialToEz));
        }

        [Test]
        public void TryInferDirection_rejects_same_kind()
        {
            var a = entry(schema: 51_006);
            var b = entry(schema: 51_003);

            Assert.That(RealmSyncDirectionHelper.TryInferDirection(a, b, out _, out string? error), Is.False);
            Assert.That(error, Is.Not.Null);
        }

        private static RealmFileEntry entry(int schema) => new()
        {
            Id = "test",
            DisplayName = "test.realm",
            FilePath = @"C:\data\client.realm",
            DataDirectory = @"C:\data",
            SchemaVersion = schema,
        };
    }
}
