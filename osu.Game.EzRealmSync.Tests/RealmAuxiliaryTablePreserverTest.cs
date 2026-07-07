#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.EzRealmSync.Realm;
using osu.Game.Skinning;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmAuxiliaryTablePreserverTest
    {
        [Test]
        public void isEzOnlyProtectedSkin_recognises_builtin_ez_skins()
        {
            Assert.That(RealmAuxiliaryTablePreserver.IsEzOnlyProtectedSkin(new SkinInfo { ID = new Guid("fc372386-381d-4f8e-897a-c1d89ef39f9c") }), Is.True);
            Assert.That(RealmAuxiliaryTablePreserver.IsEzOnlyProtectedSkin(new SkinInfo { ID = new Guid("1E70839C-C0D8-4DBF-B747-0C08C89D412B") }), Is.True);
            Assert.That(RealmAuxiliaryTablePreserver.IsEzOnlyProtectedSkin(new SkinInfo { ID = new Guid("fc372386-381d-4f8e-897a-c1d89ef39f2c") }), Is.True);
            Assert.That(RealmAuxiliaryTablePreserver.IsEzOnlyProtectedSkin(new SkinInfo { ID = new Guid("CFFA69DE-B3E3-4DEE-8563-3C4F425C05D0") }), Is.False);
        }
    }
}
#endif
