using NUnit.Framework;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmWritePlanTest
    {
        [Test]
        public void TryFromEndpoints_allows_same_ez_schema()
        {
            var a = entry(51_006);
            var b = entry(51_003);

            Assert.That(RealmWritePlan.TryFromEndpoints(a, b, out var plan, out _), Is.True);
            Assert.That(plan!.LegacyDirection, Is.EqualTo(SyncDirection.EzToEz));
            Assert.That(plan.StripEzFieldsForTarget, Is.False);
        }

        [Test]
        public void TryFromEndpoints_allows_same_ppy_schema()
        {
            var a = entry(51);
            var b = entry(50);

            Assert.That(RealmWritePlan.TryFromEndpoints(a, b, out var plan, out _), Is.True);
            Assert.That(plan!.LegacyDirection, Is.EqualTo(SyncDirection.PpyToPpy));
        }

        [Test]
        public void TryFromEndpoints_strips_only_ez_to_ppy()
        {
            var ez = entry(51_006);
            var ppy = entry(51);

            Assert.That(RealmWritePlan.TryFromEndpoints(ez, ppy, out var plan, out _), Is.True);
            Assert.That(plan!.StripEzFieldsForTarget, Is.True);
            Assert.That(plan.LegacyDirection, Is.EqualTo(SyncDirection.EzToOfficial));
        }

        [Test]
        public void TryFromEndpoints_rejects_unknown_schema()
        {
            var a = entry(null);
            var b = entry(51);

            Assert.That(RealmWritePlan.TryFromEndpoints(a, b, out _, out string? error), Is.False);
            Assert.That(error, Does.Contain("A"));
        }

        private static RealmFileEntry entry(int? schema)
        {
            const string dataDir = @"C:\osu\storage\data";
            return new RealmFileEntry
            {
                Id = "test",
                DisplayName = "client.realm",
                FilePath = Path.Combine(dataDir, "client.realm"),
                DataDirectory = dataDir,
                SchemaVersion = schema,
            };
        }
    }
}
