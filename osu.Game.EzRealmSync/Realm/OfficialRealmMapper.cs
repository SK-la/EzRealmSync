#if HAS_EZ_OSU_GAME
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Scoring;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// Ez / 官方 Realm 行复制时的 BeatmapInfo 等模型字段规范。
    /// Ez→ppy：剥离 Ez 列；ppy→Ez：将源库不存在的 Ez 列归一为「待回填」哨兵（与 Ez migration 一致）。
    /// </summary>
    public static class OfficialRealmMapper
    {
        /// <summary>
        /// 写入官方 schema 目标前：丢弃 Ez 扩展列（目标磁盘无对应列）。
        /// </summary>
        public static void StripEzOnlyBeatmapFields(BeatmapInfo beatmap) => resetUnknownEzBeatmapMetadata(beatmap);

        /// <summary>
        /// 写入官方 schema 目标前：谱面集级 Ez Hosting 字段归零为 internal。
        /// </summary>
        public static void StripEzOnlyBeatmapSetFields(BeatmapSetInfo set)
        {
            set.HostingKind = BeatmapSetHostingKind.Internal;
            set.ExternalContentRoot = string.Empty;

            foreach (var beatmap in set.Beatmaps)
                StripEzOnlyBeatmapFields(beatmap);
        }

        /// <summary>
        /// 从官方 schema 源写入 Ez 目标前：缺列 Detach 可能为 0/false，归一为待回填哨兵。
        /// 写入后由 Ez <see cref="osu.Game.Beatmaps.BeatmapUpdater"/> / BackgroundDataStoreProcessor 增量回填。
        /// </summary>
        public static void NormalizeEzOnlyBeatmapFields(BeatmapInfo beatmap) => resetUnknownEzBeatmapMetadata(beatmap);

        public static void StripEzOnlyScoreFields(ScoreInfo score)
        {
            score.ManiaHitMode = -1;
            score.ManiaHealthMode = -1;
        }

        public static void StripEzOnlyRulesetFields(RulesetInfo ruleset) => ruleset.LastAppliedXxySrVersion = 0;

        private static void resetUnknownEzBeatmapMetadata(BeatmapInfo beatmap)
        {
            beatmap.XxyStarRating = -1;
            beatmap.PerformancePoints = -1;
            beatmap.HasVideo = null;
            beatmap.HasStoryboard = null;
        }
    }
}
#endif
