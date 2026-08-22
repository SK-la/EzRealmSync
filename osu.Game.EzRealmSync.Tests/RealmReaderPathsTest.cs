using NUnit.Framework;
using osu.Game.EzRealmSync.Realm.Readers;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmReaderPathsTest
    {
        private string root = null!;

        [SetUp]
        public void SetUp() => root = Path.Combine(Path.GetTempPath(), "EzRealmSyncReaderPaths", Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }

        [Test]
        public void ResolveSharedLibDirectory_official_points_to_shared_folder_when_present()
        {
            string sharedLib = RealmReaderPaths.OfficialSharedLibDirectory(root);
            Directory.CreateDirectory(sharedLib);
            File.WriteAllText(Path.Combine(sharedLib, "Realm.dll"), string.Empty);

            string? resolved = RealmReaderPaths.ResolveSharedLibDirectory("official", root);

            Assert.That(resolved, Is.EqualTo(sharedLib));
            Assert.That(RealmReaderPaths.HasOfficialSharedBaseline(root), Is.True);
        }

        [Test]
        public void ResolveSharedLibDirectory_official_returns_null_when_shared_missing()
        {
            Assert.That(RealmReaderPaths.ResolveSharedLibDirectory("official", root), Is.Null);
            Assert.That(RealmReaderPaths.HasOfficialSharedBaseline(root), Is.False);
        }
    }
}
