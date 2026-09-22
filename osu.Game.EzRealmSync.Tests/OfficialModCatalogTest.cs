using NUnit.Framework;
using osu.Game.EzRealmSync.Realm;

namespace osu.Game.EzRealmSync.Tests
{
    /// <summary>
    /// 官方 mod 名录（<c>Realm/OfficialModList.json</c>，由 <c>scripts/Generate-OfficialModList.ps1</c> 生成）
    /// 的装载与判定口径。名录是「官方认不认这条 mod」的唯一依据，装错或漏装都会让成绩互导放行官方还原不了的行。
    /// </summary>
    [TestFixture]
    public class OfficialModCatalogTest
    {
        [Test]
        public void Catalog_loads_the_official_acronyms_of_every_ruleset()
        {
            Assert.That(OfficialModCatalog.Shared, Is.Not.Empty, "共享 mod 名录是空的，嵌入资源没读到。");

            foreach (string ruleset in new[] { "osu", "mania", "taiko", "catch" })
                Assert.That(OfficialModCatalog.PerRuleset.Keys, Does.Contain(ruleset), $"缺 {ruleset} 的官方 mod 名录。");

            // 抽几个不随版本漂移的官方缩写钉住名录口径。
            Assert.That(OfficialModCatalog.Shared, Does.Contain("HD").And.Contain("DT").And.Contain("NF"));
            Assert.That(OfficialModCatalog.PerRuleset["mania"], Does.Contain("4K").And.Contain("IN").And.Contain("HO"));
            Assert.That(OfficialModCatalog.PerRuleset["osu"], Does.Contain("AL").And.Contain("ST"));
        }

        [Test]
        public void Official_lookup_is_ruleset_scoped_and_case_sensitive()
        {
            Assert.That(OfficialModCatalog.IsOfficial("mania", "HD"), Is.True, "共享 mod 在任何规则集下都该认。");
            Assert.That(OfficialModCatalog.IsOfficial("mania", "7K"), Is.True);

            Assert.That(OfficialModCatalog.IsOfficial("osu", "7K"), Is.False, "mania 专属 mod 不该在 osu 规则集下被认。");
            Assert.That(OfficialModCatalog.IsOfficial("mania", "ncl"), Is.False, "缩写比对必须区分大小写，否则官方解析不出来也算放行。");
            Assert.That(OfficialModCatalog.IsOfficial("diva", "HD"), Is.False, "官方没有的规则集一律不认。");
            Assert.That(OfficialModCatalog.IsOfficial("mania", null), Is.False);
        }

        [Test]
        public void Ez_only_mods_are_not_in_the_catalog()
        {
            // Ez 侧的私有 mod 缩写不进名录；与官方撞名的（DS / DP / GR / RP）无法靠缩写区分，
            // 已由用户确认接受（成绩互导不追求完整）。
            foreach (string ezOnly in new[] { "NCl", "AJ", "DSp", "DD", "JU" })
                Assert.That(OfficialModCatalog.IsOfficial("mania", ezOnly), Is.False, $"{ezOnly} 是 Ez 专用 mod，不该被放行。");
        }
    }
}
