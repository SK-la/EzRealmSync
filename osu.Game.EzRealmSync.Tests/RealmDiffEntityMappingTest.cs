#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.EzRealmSync;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmDiffEntityMappingTest
    {
        [Test]
        public void Roundtrip_preserves_diff_entity_fields()
        {
            var original = new RealmDiffEntity
            {
                Id = Guid.NewGuid(),
                EntityKind = EntityKind.Score,
                Hash = "abc",
                Title = "title",
                Artist = "artist",
                Ruleset = "osu",
                Date = DateTimeOffset.UtcNow,
                OnlineId = 42,
                DifficultyName = "Hard",
                CollectionBeatmapCount = 3,
                CollectionHashFingerprint = "a|b",
            };

            var restored = RealmDiffEntityMapping.FromDto(RealmDiffEntityMapping.ToDto(original));

            Assert.That(restored.Id, Is.EqualTo(original.Id));
            Assert.That(restored.EntityKind, Is.EqualTo(original.EntityKind));
            Assert.That(restored.Hash, Is.EqualTo(original.Hash));
            Assert.That(restored.Title, Is.EqualTo(original.Title));
            Assert.That(restored.CollectionHashFingerprint, Is.EqualTo(original.CollectionHashFingerprint));
        }
    }
}
#endif
