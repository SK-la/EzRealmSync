#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.EzRealmSync.Realm;
using osu.Game.Rulesets;
using osu.Game.Scoring;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class OfficialConvertScoreFilterTest
    {
        [Test]
        public void Score_with_no_mods_is_kept()
        {
            var ruleset = createOsuRuleset();
            if (!canInstantiateRuleset(ruleset))
                Assert.Ignore("Osu ruleset assembly not loadable in test host.");

            var score = createScore(ruleset, "[]");
            var exported = new HashSet<string>(StringComparer.Ordinal) { score.BeatmapHash };

            Assert.That(OfficialConvertScoreFilter.ShouldExportScore(score, exported), Is.True);
        }

        [Test]
        public void Score_with_ez_mania_mod_is_excluded()
        {
            var ruleset = createManiaRuleset();
            if (!canInstantiateRuleset(ruleset))
                Assert.Ignore("Mania ruleset assembly not loadable in test host.");

            var score = createScore(ruleset, "[{\"Acronym\":\"NCl\",\"Settings\":{}}]");
            var exported = new HashSet<string>(StringComparer.Ordinal) { score.BeatmapHash };

            Assert.That(OfficialConvertScoreFilter.ShouldExportScore(score, exported), Is.False);
        }

        [Test]
        public void Score_with_unknown_mod_is_excluded()
        {
            var ruleset = createOsuRuleset();
            if (!canInstantiateRuleset(ruleset))
                Assert.Ignore("Osu ruleset assembly not loadable in test host.");

            var score = createScore(ruleset, "[{\"Acronym\":\"ZZZZ\",\"Settings\":{}}]");
            var exported = new HashSet<string>(StringComparer.Ordinal) { score.BeatmapHash };

            Assert.That(OfficialConvertScoreFilter.ShouldExportScore(score, exported), Is.False);
        }

        [Test]
        public void Score_on_unexported_beatmap_is_excluded()
        {
            var score = createScore(createManiaRuleset(), "[]");
            var exported = new HashSet<string>(StringComparer.Ordinal);

            Assert.That(OfficialConvertScoreFilter.ShouldExportScore(score, exported), Is.False);
        }

        [Test]
        public void Diva_ruleset_is_not_exportable()
        {
            var ruleset = new RulesetInfo("diva", "DIVA", "osu.Game.Rulesets.Diva.DivaRuleset, osu.Game.Rulesets.Diva", -1);

            Assert.That(OfficialConvertRulesetFilter.ShouldExportRuleset(ruleset), Is.False);
        }

        [Test]
        public void Official_osu_ruleset_is_exportable()
        {
            var ruleset = new RulesetInfo("osu", "osu!", "osu.Game.Rulesets.Osu.OsuRuleset, osu.Game.Rulesets.Osu", 0);

            Assert.That(OfficialConvertRulesetFilter.ShouldExportRuleset(ruleset), Is.True);
        }

        private static RulesetInfo createManiaRuleset() =>
            new RulesetInfo("mania", "osu!mania", "osu.Game.Rulesets.Mania.ManiaRuleset, osu.Game.Rulesets.Mania", 3);

        private static RulesetInfo createOsuRuleset() =>
            new RulesetInfo("osu", "osu!", "osu.Game.Rulesets.Osu.OsuRuleset, osu.Game.Rulesets.Osu", 0);

        private static bool canInstantiateRuleset(RulesetInfo ruleset)
        {
            try
            {
                ruleset.CreateInstance();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static ScoreInfo createScore(RulesetInfo ruleset, string modsJson) =>
            new ScoreInfo
            {
                BeatmapHash = "abc123hash",
                Ruleset = ruleset,
                ModsJson = modsJson,
            };
    }
}
#endif
