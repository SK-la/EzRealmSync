#if HAS_EZ_OSU_GAME
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Scoring;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 将 Ez 模型字段规范为可写入官方库（schema 51）的形态。
    /// </summary>
    public static class OfficialRealmMapper
    {
        public static void StripEzOnlyBeatmapFields(BeatmapInfo beatmap)
        {
            beatmap.XxyStarRating = -1;
            beatmap.PerformancePoints = -1;
            beatmap.HasVideo = null;
            beatmap.HasStoryboard = null;
        }

        public static void StripEzOnlyScoreFields(ScoreInfo score)
        {
            score.ManiaHitMode = -1;
            score.ManiaHealthMode = -1;
        }

        public static void StripEzOnlyRulesetFields(RulesetInfo ruleset) => ruleset.LastAppliedXxySrVersion = 0;
    }
}
#endif
