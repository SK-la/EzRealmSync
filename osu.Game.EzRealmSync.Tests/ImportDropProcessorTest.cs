using NUnit.Framework;
using osu.EzRealmSync.AppModel;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class ImportDropProcessorTest
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
            }
        }

        [Test]
        public void ParseDroppedPaths_registers_realm_files()
        {
            Directory.CreateDirectory(tempRoot);
            string realm = Path.Combine(tempRoot, "client.realm");
            File.WriteAllText(realm, "x");

            var actions = ImportDropProcessor.ParseDroppedPaths(new[] { realm });
            Assert.That(actions, Has.Count.EqualTo(1));
            Assert.That(actions[0].Kind, Is.EqualTo(ImportDropActionKind.RegisterRealm));
            Assert.That(actions[0].Path, Is.EqualTo(Path.GetFullPath(realm)));
        }

        [Test]
        public void ParseDroppedPaths_sets_search_directory_for_folders()
        {
            Directory.CreateDirectory(tempRoot);
            var actions = ImportDropProcessor.ParseDroppedPaths(new[] { tempRoot });
            Assert.That(actions, Has.Count.EqualTo(1));
            Assert.That(actions[0].Kind, Is.EqualTo(ImportDropActionKind.SetEndpointAWorkspace));
        }

        [Test]
        public void ParseDroppedPaths_ignores_unknown_files()
        {
            Directory.CreateDirectory(tempRoot);
            string txt = Path.Combine(tempRoot, "readme.txt");
            File.WriteAllText(txt, "nope");

            Assert.That(ImportDropProcessor.ParseDroppedPaths(new[] { txt }), Is.Empty);
        }
    }
}
