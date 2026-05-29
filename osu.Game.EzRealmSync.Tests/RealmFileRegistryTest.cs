using NUnit.Framework;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmFileRegistryTest
    {
        private string tempRoot = null!;

        [SetUp]
        public void SetUp() => tempRoot = Path.Combine(Path.GetTempPath(), "EzRealmSyncTests", Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // 忽略清理失败
            }
        }

        [Test]
        public void Register_deduplicates_same_path()
        {
            Directory.CreateDirectory(Path.Combine(tempRoot, "data"));
            string realm = Path.Combine(tempRoot, "data", "client.realm");
            File.WriteAllText(realm, "realm");

            var registry = new RealmFileRegistry();
            var first = registry.Register(realm);
            var second = registry.Register(realm);

            Assert.That(second.Id, Is.EqualTo(first.Id));
            Assert.That(registry.List(), Has.Count.EqualTo(1));
        }

        [Test]
        public void MergeDiscovered_finds_realm_under_data_folder()
        {
            string dataDir = Path.Combine(tempRoot, "data");
            Directory.CreateDirectory(dataDir);
            string realm = Path.Combine(dataDir, "client.realm");
            File.WriteAllText(realm, "realm");

            var registry = new RealmFileRegistry();
            var files = registry.MergeDiscovered(tempRoot);

            Assert.That(files.Select(f => f.FilePath), Does.Contain(realm));
        }
    }
}
