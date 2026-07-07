using NUnit.Framework;
using osu.EzRealmSync.AppModel;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class AppSettingsStoreTest
    {
        private string settingsPath = null!;

        [SetUp]
        public void SetUp() => settingsPath = Path.Combine(Path.GetTempPath(), "EzRealmSyncTests", Guid.NewGuid().ToString("N"), "settings.json");

        [TearDown]
        public void TearDown()
        {
            try
            {
                string? dir = Path.GetDirectoryName(settingsPath);
                if (dir != null && Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // ignored
            }
        }

        [Test]
        public void SaveLoad_round_trips_settings()
        {
            var original = new EzRealmSyncAppSettings
            {
                SearchDirectory = @"D:\osu\data",
                ImportSelectedRealmId = "import",
                DataRealmId = "data",
                SyncRealmIdA = "a",
                SyncRealmIdB = "b",
                FixRealmId = "fix",
                ExportRealmId = "export",
                BackupDirectory = @"D:\backups",
                ConfirmBeforeDelete = false,
                ActiveReaderPackageId = "ez-51003",
                ReaderPackagesDirectory = @"D:\readers",
            };

            AppSettingsStore.Save(original, settingsPath);
            var loaded = AppSettingsStore.Load(settingsPath);

            Assert.That(loaded.SearchDirectory, Is.EqualTo(original.SearchDirectory));
            Assert.That(loaded.DataRealmId, Is.EqualTo(original.DataRealmId));
            Assert.That(loaded.SyncRealmIdA, Is.EqualTo(original.SyncRealmIdA));
            Assert.That(loaded.SyncRealmIdB, Is.EqualTo(original.SyncRealmIdB));
            Assert.That(loaded.FixRealmId, Is.EqualTo(original.FixRealmId));
            Assert.That(loaded.BackupDirectory, Is.EqualTo(original.BackupDirectory));
            Assert.That(loaded.ConfirmBeforeDelete, Is.False);
            Assert.That(loaded.ActiveReaderPackageId, Is.EqualTo("ez-51003"));
            Assert.That(loaded.ReaderPackagesDirectory, Is.EqualTo(@"D:\readers"));
        }

        [Test]
        public void Load_missing_file_returns_defaults()
        {
            var loaded = AppSettingsStore.Load(settingsPath);
            Assert.That(loaded.ConfirmBeforeDelete, Is.True);
            Assert.That(loaded.BackupDirectory, Is.Not.Empty);
        }

        [Test]
        public void Load_migrates_legacy_endpoint_a_to_search_directory()
        {
            var legacy = new EzRealmSyncAppSettings { EndpointAWorkspace = @"D:\legacy\ez" };
            AppSettingsStore.Save(legacy, settingsPath);

            var loaded = AppSettingsStore.Load(settingsPath);

            Assert.That(loaded.SearchDirectory, Is.EqualTo(@"D:\legacy\ez"));
        }
    }
}
