#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.Models;
using osu.Game.Rulesets;

namespace osu.Game.EzRealmSync.Realm
{
    public readonly struct RealmMigrationCounts
    {
        public int RealmFiles { get; init; }

        public int BeatmapSets { get; init; }

        public int Skins { get; init; }

        public int Rulesets { get; init; }

        public int Scores { get; init; }

        public static RealmMigrationCounts Capture(RealmAccess access)
        {
            RealmMigrationCounts counts = default;

            access.Run(realm =>
            {
                counts = new RealmMigrationCounts
                {
                    RealmFiles = realm.All<RealmFile>().Count(),
                    BeatmapSets = realm.LiveBeatmapSets().Count(),
                    Skins = realm.LiveSkins().Count(),
                    Rulesets = realm.All<RulesetInfo>().Count(),
                    Scores = realm.LiveScores().Count(),
                };
            });

            return counts;
        }

        public bool IsCatastrophicLossComparedTo(RealmMigrationCounts before)
        {
            if (before.RealmFiles >= 1_000 && RealmFiles < Math.Max(100, before.RealmFiles / 10))
                return true;

            if (before.BeatmapSets >= 10 && BeatmapSets < before.BeatmapSets * 0.9)
                return true;

            if (before.Rulesets >= 4 && Rulesets == 0)
                return true;

            if (before.Skins >= 1 && Skins == 0)
                return true;

            if (before.Scores >= 100 && Scores < before.Scores * 0.9)
                return true;

            return false;
        }

        public override string ToString() =>
            $"files={RealmFiles}, sets={BeatmapSets}, skins={Skins}, rulesets={Rulesets}, scores={Scores}";
    }
}
#endif
