namespace osu.Game.EzRealmSync.Realm
{
    internal static class RealmDiffConflictRules
    {
        public static string? GetConflictSummary(RealmDiffEntity source, RealmDiffEntity target)
        {
            if (source.EntityKind != target.EntityKind)
                return "实体类型不一致";

            return source.EntityKind switch
            {
                Models.EntityKind.BeatmapSet => compareBeatmapSet(source, target),
                Models.EntityKind.Beatmap => compareBeatmap(source, target),
                Models.EntityKind.Score => compareScore(source, target),
                _ => null,
            };
        }

        private static string? compareBeatmapSet(RealmDiffEntity source, RealmDiffEntity target)
        {
            var parts = new List<string>();

            if (!string.Equals(source.Hash, target.Hash, StringComparison.Ordinal))
                parts.Add("Hash");

            if (source.OnlineId != target.OnlineId)
                parts.Add("OnlineID");

            if (!string.Equals(source.Title, target.Title, StringComparison.Ordinal))
                parts.Add("标题");

            return format(parts);
        }

        private static string? compareBeatmap(RealmDiffEntity source, RealmDiffEntity target)
        {
            var parts = new List<string>();

            if (!string.Equals(source.Hash, target.Hash, StringComparison.Ordinal))
                parts.Add("Hash");

            if (!string.Equals(source.Ruleset, target.Ruleset, StringComparison.Ordinal))
                parts.Add("Ruleset");

            if (!string.Equals(source.DifficultyName, target.DifficultyName, StringComparison.Ordinal))
                parts.Add("难度名");

            return format(parts);
        }

        private static string? compareScore(RealmDiffEntity source, RealmDiffEntity target)
        {
            var parts = new List<string>();

            if (!string.Equals(source.Hash, target.Hash, StringComparison.Ordinal))
                parts.Add("Hash");

            if (!string.Equals(source.Ruleset, target.Ruleset, StringComparison.Ordinal))
                parts.Add("Ruleset");

            if (source.Date != target.Date)
                parts.Add("Date");

            return format(parts);
        }

        private static string? format(List<string> parts) =>
            parts.Count == 0 ? null : string.Join("、", parts) + " 不一致";
    }
}
