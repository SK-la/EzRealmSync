using NUnit.Framework;
using osu.Game.EzRealmSync.Realm;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class OfficialBaselineSchemaTest
    {
        [Test]
        public void Known_tables_exclude_ez_only_columns()
        {
            Assert.That(OfficialBaselineSchema.IsKnownProperty(OfficialBaselineSchema.BeatmapSet, "Hash"), Is.True);
            Assert.That(OfficialBaselineSchema.IsKnownProperty(OfficialBaselineSchema.Beatmap, "MD5Hash"), Is.True);
            Assert.That(OfficialBaselineSchema.IsKnownProperty(OfficialBaselineSchema.File, "Hash"), Is.True);
            Assert.That(OfficialBaselineSchema.IsKnownProperty(OfficialBaselineSchema.Skin, "InstantiationInfo"), Is.True);
            Assert.That(OfficialBaselineSchema.IsKnownProperty(OfficialBaselineSchema.Beatmap, "XxyStarRating"), Is.False);
            Assert.That(OfficialBaselineSchema.EzOnlyPropertyNames, Does.Contain("HostingKind"));
        }

        [Test]
        public void Ez_only_skin_and_ruleset_filters_do_not_need_osu_game()
        {
            Assert.That(OfficialBaselineSchema.IsEzOnlyRuleset("diva"), Is.True);
            Assert.That(OfficialBaselineSchema.IsEzOnlyRuleset("osu"), Is.False);
            Assert.That(
                OfficialBaselineSchema.IsEzOnlySkin(Guid.Parse("fc372386-381d-4f8e-897a-c1d89ef39f9c"), "osu.Game.EzOsuGame.ScriptedSkin.Ez2Skin", "Ez2"),
                Is.True);
            Assert.That(
                OfficialBaselineSchema.IsEzOnlySkin(Guid.NewGuid(), "osu.Game.Skinning.LegacySkin", "Default"),
                Is.False);
        }
    }
}
