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
        public void ResolveClientRealmPath_prefers_ez_versioned_sidecar()
        {
            string ezRoot = Path.Combine(Path.GetTempPath(), "EzRealmSyncTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ezRoot);
            Directory.CreateDirectory(Path.Combine(ezRoot, "files"));
            File.WriteAllText(Path.Combine(ezRoot, "client.realm"), "mock");
            File.WriteAllText(Path.Combine(ezRoot, "client_51.realm"), "mock51");
            File.WriteAllText(Path.Combine(ezRoot, "client_51007.realm"), "mock51007");

            try
            {
                string fullRoot = Path.GetFullPath(ezRoot);
                Assert.That(RealmWorkspacePaths.ResolveClientRealmPath(ezRoot), Is.EqualTo(Path.Combine(fullRoot, "client_51007.realm")));
            }
            finally
            {
                if (Directory.Exists(ezRoot))
                    Directory.Delete(ezRoot, recursive: true);
            }
        }

        [Test]
        public void ResolveClientRealmPath_falls_back_to_legacy_client_51_when_no_ez_sidecar()
        {
            string ezRoot = Path.Combine(Path.GetTempPath(), "EzRealmSyncTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ezRoot);
            Directory.CreateDirectory(Path.Combine(ezRoot, "files"));
            File.WriteAllText(Path.Combine(ezRoot, "client_51.realm"), "mock51");

            try
            {
                string fullRoot = Path.GetFullPath(ezRoot);
                Assert.That(RealmWorkspacePaths.ResolveClientRealmPath(ezRoot), Is.EqualTo(Path.Combine(fullRoot, "client_51.realm")));
            }
            finally
            {
                if (Directory.Exists(ezRoot))
                    Directory.Delete(ezRoot, recursive: true);
            }
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

        [Test]
        public void ResolveStorageRelativeRealmPath_preserves_data_segment()
        {
            string realmPath = Path.Combine(workspace, "data", "client.realm");
            Assert.That(RealmWorkspacePaths.ResolveStorageRelativeRealmPath(realmPath), Is.EqualTo(Path.Combine("data", "client.realm")));
        }

        [Test]
        public void ResolveStorageRelativeRealmPath_root_level_realm()
        {
            string ezRoot = Path.Combine(Path.GetTempPath(), "EzRealmSyncTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ezRoot);
            string realmPath = Path.Combine(ezRoot, "client_51007.realm");
            File.WriteAllText(realmPath, "mock");

            try
            {
                Assert.That(RealmWorkspacePaths.ResolveStorageRelativeRealmPath(realmPath), Is.EqualTo("client_51007.realm"));
            }
            finally
            {
                if (Directory.Exists(ezRoot))
                    Directory.Delete(ezRoot, recursive: true);
            }
        }

        [Test]
        public void TryFromEndpoints_delete_uses_same_source_and_target()
        {
            var entry = new RealmFileEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                FilePath = Path.Combine(workspace, "data", "client.realm"),
                DisplayName = "client.realm",
                SchemaVersion = 51_007,
            };

            Assert.That(RealmWritePlan.TryFromEndpoints(entry, entry, out var plan, out string? error), Is.True);
            Assert.That(error, Is.Null);
            Assert.That(plan, Is.Not.Null);
            Assert.That(plan!.SourceRealmFilePath, Is.EqualTo(plan.TargetRealmFilePath));
            Assert.That(plan.LegacyDirection, Is.EqualTo(SyncDirection.EzToEz));
        }
    }
}
