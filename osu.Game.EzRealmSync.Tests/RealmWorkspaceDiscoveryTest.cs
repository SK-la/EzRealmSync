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
