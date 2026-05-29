using NUnit.Framework;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmWorkspacePathsTest
    {
        private string workspace = null!;

        [SetUp]
        public void SetUp()
        {
            workspace = Path.Combine(Path.GetTempPath(), "EzRealmSyncTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(workspace, "data"));
            Directory.CreateDirectory(Path.Combine(workspace, "files"));
            File.WriteAllText(Path.Combine(workspace, "data", "client.realm"), "mock");
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(workspace))
                    Directory.Delete(Path.GetDirectoryName(workspace)!, recursive: true);
            }
            catch
            {
            }
        }

        [Test]
        public void FindRealmFiles_finds_data_client_realm()
        {
            var files = RealmWorkspacePaths.FindRealmFiles(workspace);
            Assert.That(files, Has.Count.EqualTo(1));
            Assert.That(files[0], Does.EndWith("client.realm"));
        }

        [Test]
        public void FindRealmFiles_finds_root_level_realms_ez_layout()
        {
            string ezRoot = Path.Combine(Path.GetTempPath(), "EzRealmSyncTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ezRoot);
            Directory.CreateDirectory(Path.Combine(ezRoot, "files"));
            File.WriteAllText(Path.Combine(ezRoot, "client.realm"), "mock");
            File.WriteAllText(Path.Combine(ezRoot, "client_master.realm"), "mock2");

            try
            {
                var files = RealmWorkspacePaths.FindRealmFiles(ezRoot);

                Assert.That(files, Has.Count.EqualTo(2));
                string fullRoot = Path.GetFullPath(ezRoot);
                Assert.That(files.All(f => string.Equals(Path.GetDirectoryName(f), fullRoot, StringComparison.OrdinalIgnoreCase)), Is.True);
                Assert.That(RealmWorkspacePaths.ResolveClientRealmPath(ezRoot), Is.EqualTo(Path.Combine(fullRoot, "client.realm")));
                Assert.That(RealmWorkspacePaths.TryResolveFilesDirectory(ezRoot, out string filesDir), Is.True);
                Assert.That(filesDir, Is.EqualTo(Path.Combine(fullRoot, "files")));
                Assert.That(RealmWorkspacePaths.ResolveStorageRoot(files[0]), Is.EqualTo(fullRoot));
            }
            finally
            {
                if (Directory.Exists(ezRoot))
                    Directory.Delete(ezRoot, recursive: true);
            }
        }

        [Test]
        public void TryResolveFilesDirectory_from_storage_root()
        {
            Assert.That(RealmWorkspacePaths.TryResolveFilesDirectory(workspace, out string filesDir), Is.True);
            Assert.That(filesDir, Is.EqualTo(Path.Combine(workspace, "files")));
        }

        [Test]
        public void ResolveStorageRoot_from_data_realm_path()
        {
            string realmPath = Path.Combine(workspace, "data", "client.realm");
            Assert.That(RealmWorkspacePaths.ResolveStorageRoot(realmPath), Is.EqualTo(workspace));
        }
    }
}
