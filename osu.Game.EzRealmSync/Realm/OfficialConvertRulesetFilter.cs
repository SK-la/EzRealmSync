#if HAS_EZ_OSU_GAME
using osu.Game.Beatmaps;
using osu.Game.Rulesets;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 转官方时排除 Ez 独有规则集、外部托管谱面集等。
    /// </summary>
    public static class OfficialConvertRulesetFilter
    {
        private static readonly HashSet<string> ez_only_ruleset_short_names = new(StringComparer.OrdinalIgnoreCase)
        {
            "diva",
            "bms",
        };

        public static bool ShouldExportRuleset(RulesetInfo ruleset)
        {
            if (string.IsNullOrWhiteSpace(ruleset.ShortName))
                return false;

            if (ez_only_ruleset_short_names.Contains(ruleset.ShortName))
                return false;

            if (tryCreateOfficialRuleset(ruleset) != null)
                return true;

            return isKnownOfficialInstantiation(ruleset.InstantiationInfo);
        }

        public static bool ShouldExportBeatmapSet(BeatmapSetInfo set)
        {
            if (set.DeletePending)
                return false;

            if (set.HostingKind == BeatmapSetHostingKind.External)
                return false;

            foreach (var beatmap in set.Beatmaps)
            {
                if (beatmap.Hidden)
                    continue;

                if (!ShouldExportBeatmapRuleset(beatmap.Ruleset))
                    return false;
            }

            return set.Beatmaps.Any(b => !b.Hidden);
        }

        public static bool ShouldExportBeatmapRuleset(RulesetInfo ruleset) => ShouldExportRuleset(ruleset);

        private static Ruleset? tryCreateOfficialRuleset(RulesetInfo ruleset)
        {
            try
            {
                var instance = ruleset.CreateInstance();
                return isOfficialRulesetAssembly(instance.GetType().Assembly.GetName().Name) ? instance : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool isOfficialRulesetAssembly(string? assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
                return false;

            if (!assemblyName.StartsWith("osu.Game.Rulesets.", StringComparison.Ordinal))
                return false;

            if (assemblyName.Contains("Diva", StringComparison.OrdinalIgnoreCase))
                return false;

            if (assemblyName.Contains("BMS", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private static bool isKnownOfficialInstantiation(string instantiationInfo)
        {
            if (string.IsNullOrWhiteSpace(instantiationInfo))
                return false;

            if (instantiationInfo.Contains("Rulesets.Diva", StringComparison.OrdinalIgnoreCase)
                || instantiationInfo.Contains("Rulesets.BMS", StringComparison.OrdinalIgnoreCase))
                return false;

            return instantiationInfo.Contains("Rulesets.Osu.", StringComparison.Ordinal)
                   || instantiationInfo.Contains("Rulesets.Mania.", StringComparison.Ordinal)
                   || instantiationInfo.Contains("Rulesets.Taiko.", StringComparison.Ordinal)
                   || instantiationInfo.Contains("Rulesets.Catch.", StringComparison.Ordinal);
        }
    }
}
#endif
