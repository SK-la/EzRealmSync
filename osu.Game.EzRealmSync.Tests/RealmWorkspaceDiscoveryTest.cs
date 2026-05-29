using NUnit.Framework;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmWorkspaceDiscoveryTest
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
        public void NormalizeStorageRoot_maps_data_subfolder_to_parent()
        {
            string storage = Path.Combine(tempRoot, "osu");
            string dataDir = Path.Combine(storage, "data");
            Directory.CreateDirectory(dataDir);

            Assert.That(RealmWorkspaceDiscovery.NormalizeStorageRoot(dataDir), Is.EqualTo(Path.GetFullPath(storage)));
        }

        [Test]
        public void FindRealmFilesInSearchDirectory_finds_ez_root_layout()
        {
            string storage = Path.Combine(tempRoot, "EZ2OSU-lazer");
            Directory.CreateDirectory(storage);
            Directory.CreateDirectory(Path.Combine(storage, "files"));
            File.WriteAllText(Path.Combine(storage, "client.realm"), "x");

            var files = RealmWorkspaceDiscovery.FindRealmFilesInSearchDirectory(storage);

            Assert.That(files, Has.Count.EqualTo(1));
            Assert.That(RealmWorkspaceDiscovery.TryResolveSharedFilesDirectory(storage, out _), Is.True);
        }

        [Test]
        public void TryResolveSharedFilesDirectory_uses_storage_root_files()
        {
            string storage = Path.Combine(tempRoot, "osu");
            Directory.CreateDirectory(Path.Combine(storage, "data"));
            Directory.CreateDirectory(Path.Combine(storage, "files"));

            Assert.That(RealmWorkspaceDiscovery.TryResolveSharedFilesDirectory(storage, out string files), Is.True);
            Assert.That(files, Is.EqualTo(Path.Combine(Path.GetFullPath(storage), "files")));
        }

        [Test]
        public void TryResolveFilesDirectoryForRealm_prefers_search_directory_files()
        {
            string storage = Path.Combine(tempRoot, "osu");
            string other = Path.Combine(tempRoot, "other");
            Directory.CreateDirectory(Path.Combine(storage, "data"));
            Directory.CreateDirectory(Path.Combine(storage, "files"));
            Directory.CreateDirectory(Path.Combine(other, "files"));
            string realm = Path.Combine(storage, "data", "client.realm");
            File.WriteAllText(realm, "x");

            Assert.That(
                RealmWorkspaceDiscovery.TryResolveFilesDirectoryForRealm(storage, realm, out string files),
                Is.True);
            Assert.That(files, Is.EqualTo(Path.Combine(Path.GetFullPath(storage), "files")));
        }

        [Test]
        public void FindRealmFilesInWorkspaces_merges_A_and_B()
        {
            string workspaceA = Path.Combine(tempRoot, "a");
            string workspaceB = Path.Combine(tempRoot, "b");
            Directory.CreateDirectory(Path.Combine(workspaceA, "data"));
            Directory.CreateDirectory(Path.Combine(workspaceB, "data"));
            File.WriteAllText(Path.Combine(workspaceA, "data", "client.realm"), "a");
            File.WriteAllText(Path.Combine(workspaceB, "data", "client.realm"), "b");

            var files = RealmWorkspaceDiscovery.FindRealmFilesInWorkspaces(workspaceA, workspaceB);

            Assert.That(files, Has.Count.EqualTo(2));
        }
    }
}
