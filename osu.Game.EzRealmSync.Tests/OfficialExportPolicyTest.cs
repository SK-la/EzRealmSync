using NUnit.Framework;
using osu.Game.EzRealmSync.Realm;

namespace osu.Game.EzRealmSync.Tests
{
    /// <summary>
    /// 「官方客户端能不能用这一行」的判据。口径是能少不能错：官方还原不了的成绩 / 谱面宁可留在这边，
    /// 也不写过去变成官方端看不见或语义错误的行。
    /// </summary>
    [TestFixture]
    public class OfficialExportPolicyTest
    {
        private const string official_ruleset = "osu";
        private const string ez_ruleset = "diva";

        [Test]
        public void Ez_only_rulesets_are_never_official()
        {
            Assert.That(OfficialExportPolicy.IsOfficialRuleset("osu", "osu.Game.Rulesets.Osu.OsuRuleset, osu.Game.Rulesets.Osu"), Is.True);
            Assert.That(OfficialExportPolicy.IsOfficialRuleset("mania", "osu.Game.Rulesets.Mania.ManiaRuleset, osu.Game.Rulesets.Mania"), Is.True);
            Assert.That(OfficialExportPolicy.IsOfficialRuleset(ez_ruleset, "osu.Game.Rulesets.Diva.DivaRuleset, osu.Game.Rulesets.DIVA"), Is.False);
            Assert.That(OfficialExportPolicy.IsOfficialRuleset("bms", "osu.Game.Rulesets.BMS.BMSRuleset, osu.Game.Rulesets.BMS"), Is.False);
            Assert.That(OfficialExportPolicy.IsOfficialRuleset("", "osu.Game.Rulesets.Osu.OsuRuleset"), Is.False, "没有短名的规则集官方建不出来。");

            // 官方四规则集之外的短名，实例化信息指向官方程序集也算官方（目标库可能已有这种行）。
            Assert.That(OfficialExportPolicy.IsOfficialRuleset("fruits", "osu.Game.Rulesets.Catch.CatchRuleset, osu.Game.Rulesets.Catch"), Is.True);
            Assert.That(OfficialExportPolicy.IsOfficialRuleset("fruits", null), Is.False);
        }

        [Test]
        public void Externally_hosted_sets_and_sets_without_a_usable_difficulty_are_not_official()
        {
            var usable = new BeatmapRowFacts(Hidden: false, RulesetIsOfficial: true);

            Assert.That(OfficialExportPolicy.IsOfficialBeatmapSet(0, [usable]), Is.True);
            Assert.That(OfficialExportPolicy.IsOfficialBeatmapSet(1, [usable]), Is.False, "外部托管的集内容在 Ez 的磁盘目录，官方读不到。");
            Assert.That(OfficialExportPolicy.IsOfficialBeatmapSet(0, []), Is.False, "没有难度的集搬过去就是空集。");
            Assert.That(
                OfficialExportPolicy.IsOfficialBeatmapSet(0, [new BeatmapRowFacts(Hidden: true, RulesetIsOfficial: true)]),
                Is.False,
                "只有隐藏难度的集在官方端同样是空集。");
            Assert.That(
                OfficialExportPolicy.IsOfficialBeatmapSet(0, [new BeatmapRowFacts(Hidden: false, RulesetIsOfficial: false)]),
                Is.False,
                "只含 Ez 规则集难度的集官方打不开。");
            Assert.That(
                OfficialExportPolicy.IsOfficialBeatmapSet(0, [new BeatmapRowFacts(Hidden: false, RulesetIsOfficial: false), usable]),
                Is.True,
                "有一条官方难度的集可以搬。");
        }

        [Test]
        public void Scores_need_official_ruleset_official_mods_and_lazer_judgement()
        {
            Assert.That(OfficialExportPolicy.IsOfficialScore(official_ruleset, 0, 0, """[{"acronym":"HD"}]"""), Is.True, "客户端存的是小写 acronym，不能因为大小写把人家的成绩丢掉。");
            Assert.That(OfficialExportPolicy.IsOfficialScore(official_ruleset, 0, 0, """[{"Acronym":"HD"}]"""), Is.True, "大小写不同的同名字段同样要认。");
            Assert.That(OfficialExportPolicy.IsOfficialScore(official_ruleset, 0, 0, ""), Is.True, "没有 mod 的成绩官方当然能还原。");

            Assert.That(OfficialExportPolicy.IsOfficialScore(ez_ruleset, 0, 0, ""), Is.False, "Ez 专用规则集的成绩官方打不开。");
            Assert.That(OfficialExportPolicy.IsOfficialScore(official_ruleset, 2, 0, ""), Is.False, "Ez 判定语义的成绩官方重算会算错。");
            Assert.That(OfficialExportPolicy.IsOfficialScore(official_ruleset, 0, 3, ""), Is.False, "Ez 血条语义的成绩同样不搬。");

            // -1 是 Ez v1 迁移前的老成绩，客户端按 Lazer 读，不算「非官方语义」。
            Assert.That(OfficialExportPolicy.IsOfficialScore(official_ruleset, -1, -1, ""), Is.True);

            Assert.That(OfficialExportPolicy.IsOfficialScore(official_ruleset, 0, 0, """[{"Acronym":"NCl"}]"""), Is.False, "Ez 专用 mod 官方解析不出来。");
            Assert.That(OfficialExportPolicy.IsOfficialScore(official_ruleset, 0, 0, """[{"Acronym":"7K"}]"""), Is.False, "别家规则集的官方 mod 在本规则集下仍是未知 mod。");
            Assert.That(OfficialExportPolicy.IsOfficialScore(official_ruleset, 0, 0, "not json"), Is.False, "mods 解析不了就不能保证官方能还原。");
            Assert.That(OfficialExportPolicy.IsOfficialScore(official_ruleset, 0, 0, """[{"Acronym":"HD"},{"Acronym":""}]"""), Is.False, "空缩写的 mod 官方也解析不出来。");
            Assert.That(OfficialExportPolicy.IsOfficialScore(official_ruleset, 0, 0, """{"Acronym":"HD"}"""), Is.False, "mods 不是数组说明数据已经不是官方形状。");
        }
    }
}
