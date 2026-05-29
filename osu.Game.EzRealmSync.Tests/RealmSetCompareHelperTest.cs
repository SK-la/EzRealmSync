using NUnit.Framework;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmSetCompareHelperTest
    {
        private static ScanResult sampleDiff() => new()
        {
            SourceOnly = new List<DiffItem> { new() { Id = Guid.NewGuid(), Category = DiffCategory.SourceOnly, EntityKind = EntityKind.Beatmap } },
            TargetOnly = new List<DiffItem> { new() { Id = Guid.NewGuid(), Category = DiffCategory.TargetOnly, EntityKind = EntityKind.Score } },
            Conflicted = new List<DiffItem> { new() { Id = Guid.NewGuid(), Category = DiffCategory.Conflicted, EntityKind = EntityKind.BeatmapSet } },
        };

        [Test]
        public void SymmetricDifference_omits_conflicted()
        {
            var result = RealmSetCompareHelper.ApplyOperation(sampleDiff(), RealmSetOperation.SymmetricDifference);

            Assert.That(result.SourceOnly, Has.Count.EqualTo(1));
            Assert.That(result.TargetOnly, Has.Count.EqualTo(1));
            Assert.That(result.Conflicted, Is.Empty);
        }

        [Test]
        public void Intersection_keeps_only_conflicted()
        {
            var result = RealmSetCompareHelper.ApplyOperation(sampleDiff(), RealmSetOperation.Intersection);

            Assert.That(result.Conflicted, Has.Count.EqualTo(1));
            Assert.That(result.SourceOnly, Is.Empty);
            Assert.That(result.TargetOnly, Is.Empty);
        }

        [Test]
        public void ToEntityKinds_maps_filter()
        {
            Assert.That(RealmSetCompareHelper.ToEntityKinds(EntityKindFilter.Score), Is.EqualTo(new[] { EntityKind.Score }));
        }
    }
}
