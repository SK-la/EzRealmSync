#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.EzOsuGame.ScriptedSkin;
using osu.Game.EzRealmSync.Realm;
using osu.Game.Extensions;
using osu.Game.Skinning;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class OfficialConvertSkinFilterTest
    {
        [Test]
        public void Ez_builtin_skins_are_excluded()
        {
            Assert.That(OfficialConvertSkinFilter.ShouldExcludeFromOfficial(createSkin(ez2_skin_id, typeof(Ez2Skin))), Is.True);
            Assert.That(OfficialConvertSkinFilter.ShouldExcludeFromOfficial(createSkin(ez_style_pro_skin_id, typeof(EzStyleProSkin))), Is.True);
            Assert.That(OfficialConvertSkinFilter.ShouldExcludeFromOfficial(createSkin(sbi_skin_id, typeof(SbISkin))), Is.True);
        }

        private static readonly Guid ez2_skin_id = new Guid("fc372386-381d-4f8e-897a-c1d89ef39f9c");
        private static readonly Guid ez_style_pro_skin_id = new Guid("1E70839C-C0D8-4DBF-B747-0C08C89D412B");
        private static readonly Guid sbi_skin_id = new Guid("fc372386-381d-4f8e-897a-c1d89ef39f2c");

        [Test]
        public void Scripted_skin_is_excluded()
        {
            var skin = new SkinInfo("script test", "author", typeof(ScriptedSkinWrapper).GetInvariantInstantiationInfo())
            {
                ID = Guid.NewGuid(),
            };

            Assert.That(ScriptedSkinSupport.IsScriptedSkin(skin), Is.True);
            Assert.That(OfficialConvertSkinFilter.ShouldExcludeFromOfficial(skin), Is.True);
        }

        [Test]
        public void Legacy_user_skin_is_kept()
        {
            var skin = new SkinInfo("user skin", "author", typeof(LegacySkin).GetInvariantInstantiationInfo())
            {
                ID = Guid.NewGuid(),
            };

            Assert.That(OfficialConvertSkinFilter.ShouldExcludeFromOfficial(skin), Is.False);
        }

        private static SkinInfo createSkin(Guid id, Type skinType) =>
            new SkinInfo("test", "author", skinType.GetInvariantInstantiationInfo())
            {
                ID = id,
            };
    }
}
#endif
