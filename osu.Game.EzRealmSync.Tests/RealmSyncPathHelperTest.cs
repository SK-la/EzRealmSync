using NUnit.Framework;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmSyncPathHelperTest
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
        public void ResolveClientRealmPath_finds_data_client_realm()
        {
            Assert.That(RealmWorkspacePaths.ResolveClientRealmPath(workspace), Does.EndWith("data\\client.realm").Or.EndWith("data/client.realm"));
        }

        [Test]
        public void TryValidateRealmFileAccessible_succeeds_for_readable_file()
        {
            string path = RealmWorkspacePaths.ResolveClientRealmPath(workspace);
            Assert.That(RealmSyncPathHelper.TryValidateRealmFileAccessible(path, out _), Is.True);
        }

        [Test]
        public void SharedFilesDirectoriesMatch_same_workspace()
        {
            Assert.That(RealmSyncPathHelper.SharedFilesDirectoriesMatch(workspace, workspace), Is.True);
        }
    }
}
