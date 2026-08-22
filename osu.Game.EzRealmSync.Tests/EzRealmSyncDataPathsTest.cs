using NUnit.Framework;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class EzRealmSyncDataPathsTest
    {
        [Test]
        public void Standard_directories_are_under_application_root()
        {
            string root = EzRealmSyncDataPaths.ApplicationRoot;

            Assert.That(EzRealmSyncDataPaths.SettingsFile, Does.StartWith(root));
            Assert.That(EzRealmSyncDataPaths.ReadersDirectory, Is.EqualTo(Path.Combine(root, "readers")));
            Assert.That(EzRealmSyncDataPaths.BackupsDirectory, Is.EqualTo(Path.Combine(root, "backups")));
            Assert.That(EzRealmSyncDataPaths.ExportsDirectory, Is.EqualTo(Path.Combine(root, "exports")));
            Assert.That(EzRealmSyncDataPaths.TempDirectory, Is.EqualTo(Path.Combine(root, "temp")));
        }
    }
}
