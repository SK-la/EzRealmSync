#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.Models;
using osu.Game.Rulesets;
using osu.Game.Skinning;

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
                    Skins = realm.All<SkinInfo>().Count(s => !s.DeletePending),
                    Rulesets = realm.All<RulesetInfo>().Count(),
                    Scores = realm.LiveScores().Count(),
                };
            });

            return counts;
        }

        public bool IsCatastrophicLossComparedTo(RealmMigrationCounts before)
        {
            if (before.RealmFiles > 0 && RealmFiles < before.RealmFiles * 0.99)
                return true;

            if (before.BeatmapSets > 0 && BeatmapSets < before.BeatmapSets * 0.99)
                return true;

            if (before.Rulesets > 0 && Rulesets < before.Rulesets * 0.99)
                return true;

            if (before.Skins > 0 && Skins < before.Skins * 0.99)
                return true;

            if (before.Scores > 0 && Scores < before.Scores * 0.99)
                return true;

            return false;
        }

        public override string ToString() =>
            $"files={RealmFiles}, sets={BeatmapSets}, skins={Skins}, rulesets={Rulesets}, scores={Scores}";
    }
}
#endif
