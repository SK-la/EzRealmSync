using NUnit.Framework;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmFilePathHelperTest
    {
        [Test]
        public void GetStoragePath_matches_osu_layout()
        {
            const string hash = "0123456789abcdef0123456789abcdef";
            string path = RealmFilePathHelper.GetStoragePath(hash);

            Assert.That(path, Is.EqualTo(Path.Combine(hash.Remove(1), hash.Remove(2), hash)));
        }
    }
}
