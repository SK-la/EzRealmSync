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

            Assert.That(EzRealmSyncDataPaths.HostSettingsFile, Is.EqualTo(Path.Combine(root, "settings.json")));
            Assert.That(EzRealmSyncDataPaths.ReadersDirectory, Is.EqualTo(Path.Combine(root, "readers")));
            Assert.That(EzRealmSyncDataPaths.BackupsDirectory, Is.EqualTo(Path.Combine(root, "backups")));
            Assert.That(EzRealmSyncDataPaths.ExportsDirectory, Is.EqualTo(Path.Combine(root, "exports")));
            Assert.That(EzRealmSyncDataPaths.TempDirectory, Is.EqualTo(Path.Combine(root, "temp")));
            Assert.That(EzRealmSyncDataPaths.LogsDirectory, Is.EqualTo(Path.Combine(root, "log")));
            Assert.That(EzRealmSyncDataPaths.DefaultRuntimeLibDirectory, Is.EqualTo(root));
        }

        [Test]
        public void ResolveConfiguredPath_expands_relative_to_application_root()
        {
            string? readers = EzRealmSyncDataPaths.ResolveConfiguredPath("readers");
            Assert.That(readers, Is.EqualTo(EzRealmSyncDataPaths.ReadersDirectory));
        }
    }
}
