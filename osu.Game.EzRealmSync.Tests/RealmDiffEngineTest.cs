using NUnit.Framework;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmDiffEngineTest
    {
        private static readonly Guid setA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid setB = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid bmOnlyEz = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid bmConflict = Guid.Parse("44444444-4444-4444-4444-444444444444");

        [Test]
        public void Compare_source_only_target_only_and_conflicted()
        {
            var source = new RealmDiffSnapshot
            {
                Entities = new[]
                {
                    entity(setA, EntityKind.BeatmapSet, "hash-a", "Title A"),
                    entity(bmOnlyEz, EntityKind.Beatmap, "only-ez", "Ez Only", ruleset: "osu"),
                    entity(bmConflict, EntityKind.Beatmap, "conflict-src", "Same", ruleset: "osu"),
                },
            };

            var target = new RealmDiffSnapshot
            {
                Entities = new[]
                {
                    entity(setB, EntityKind.BeatmapSet, "hash-b", "Title B"),
                    entity(bmConflict, EntityKind.Beatmap, "conflict-dst", "Same", ruleset: "mania"),
                },
            };

            var result = RealmDiffEngine.Compare(source, target, Array.Empty<EntityKind>());

            Assert.That(result.SourceOnly.Select(i => i.Id), Is.EquivalentTo(new[] { setA, bmOnlyEz }));
            Assert.That(result.TargetOnly.Select(i => i.Id), Is.EquivalentTo(new[] { setB }));
            Assert.That(result.Conflicted, Has.Count.EqualTo(1));
            Assert.That(result.Conflicted[0].Id, Is.EqualTo(bmConflict));
            Assert.That(result.Conflicted[0].CanApplyToTarget, Is.False);
        }

        [Test]
        public void Compare_respects_entity_kind_filter()
        {
            var source = new RealmDiffSnapshot
            {
                Entities = new[]
                {
                    entity(setA, EntityKind.BeatmapSet, "a", "Set"),
                    entity(bmOnlyEz, EntityKind.Beatmap, "b", "Bm"),
                },
            };

            var target = new RealmDiffSnapshot { Entities = Array.Empty<RealmDiffEntity>() };

            var result = RealmDiffEngine.Compare(source, target, new[] { EntityKind.Beatmap });

            Assert.That(result.SourceOnly, Has.Count.EqualTo(1));
            Assert.That(result.SourceOnly[0].EntityKind, Is.EqualTo(EntityKind.Beatmap));
        }

        private static RealmDiffEntity entity(Guid id, EntityKind kind, string hash, string title, string ruleset = "") => new()
        {
            Id = id,
            EntityKind = kind,
            Hash = hash,
            Title = title,
            Ruleset = ruleset,
        };
    }
}
