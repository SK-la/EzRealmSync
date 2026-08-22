#if HAS_EZ_OSU_GAME
using Newtonsoft.Json;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Utils;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 转官方时排除含 Ez-only mod 或官方无法解析 mod 的成绩。
    /// </summary>
    public static class OfficialConvertScoreFilter
    {
        public static bool ShouldExportScore(ScoreInfo score, IReadOnlySet<string> exportedBeatmapHashes)
        {
            if (score.DeletePending)
                return false;

            if (!OfficialConvertRulesetFilter.ShouldExportRuleset(score.Ruleset))
                return false;

            if (!string.IsNullOrEmpty(score.BeatmapHash) && !exportedBeatmapHashes.Contains(score.BeatmapHash))
                return false;

            return areModsOfficialCompatible(score);
        }

        private static bool areModsOfficialCompatible(ScoreInfo score)
        {
            Ruleset ruleset;

            try
            {
                ruleset = score.Ruleset.CreateInstance();
            }
            catch
            {
                return false;
            }

            if (!tryDeserializeApiMods(score.ModsJson, out APIMod[] apiMods))
                return false;

            var resolved = new List<Mod>();

            foreach (var apiMod in apiMods)
            {
                Mod mod;

                try
                {
                    mod = apiMod.ToMod(ruleset);
                }
                catch
                {
                    return false;
                }

                if (mod is UnknownMod)
                    return false;

                if (isEzOnlyMod(mod))
                    return false;

                resolved.Add(mod);
            }

            return ModUtils.CheckValidForGameplay(resolved, out _);
        }

        private static bool tryDeserializeApiMods(string modsJson, out APIMod[] apiMods)
        {
            if (string.IsNullOrWhiteSpace(modsJson))
            {
                apiMods = Array.Empty<APIMod>();
                return true;
            }

            try
            {
                apiMods = JsonConvert.DeserializeObject<APIMod[]>(modsJson) ?? Array.Empty<APIMod>();
                return true;
            }
            catch (JsonException)
            {
                apiMods = Array.Empty<APIMod>();
                return false;
            }
        }

        private static bool isEzOnlyMod(Mod mod)
        {
            string? ns = mod.GetType().Namespace;

            if (string.IsNullOrEmpty(ns))
                return false;

            return ns.Contains("EzOsuGame", StringComparison.Ordinal)
                   || ns.Contains("EzMania", StringComparison.Ordinal)
                   || ns.Contains("Rulesets.Diva", StringComparison.Ordinal)
                   || ns.Contains("Rulesets.BMS", StringComparison.Ordinal);
        }
    }
}
#endif
