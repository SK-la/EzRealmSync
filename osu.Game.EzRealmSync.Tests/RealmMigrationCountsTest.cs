#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.EzRealmSync.Realm;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmMigrationCountsTest
    {
        [Test]
        public void IsCatastrophicLoss_detects_massive_file_index_drop()
        {
            var before = new RealmMigrationCounts { RealmFiles = 220_000, BeatmapSets = 10_000, Rulesets = 8, Skins = 5 };
            var after = new RealmMigrationCounts { RealmFiles = 200, BeatmapSets = 10_000, Rulesets = 8, Skins = 5 };

            Assert.That(after.IsCatastrophicLossComparedTo(before), Is.True);
        }

        [Test]
        public void IsCatastrophicLoss_allows_minor_file_drift()
        {
            var before = new RealmMigrationCounts { RealmFiles = 1_000, BeatmapSets = 100, Rulesets = 8, Skins = 3 };
            var after = new RealmMigrationCounts { RealmFiles = 980, BeatmapSets = 100, Rulesets = 8, Skins = 3 };

            Assert.That(after.IsCatastrophicLossComparedTo(before), Is.False);
        }
    }
}
#endif
