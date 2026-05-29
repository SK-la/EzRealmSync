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
            }
        }

        [Test]
        public void SaveLoad_round_trips_settings()
        {
            var original = new EzRealmSyncAppSettings
            {
                SearchDirectory = @"D:\osu\legacy",
                EndpointAWorkspace = @"D:\osu\ez",
                EndpointBWorkspace = @"D:\osu\ppy",
                BackupDirectory = @"D:\backups",
                ConfirmBeforeDelete = false,
                IllegalCharacterReplacement = "-",
            };

            AppSettingsStore.Save(original, settingsPath);
            var loaded = AppSettingsStore.Load(settingsPath);

            Assert.That(loaded.EndpointAWorkspace, Is.EqualTo(original.EndpointAWorkspace));
            Assert.That(loaded.EndpointBWorkspace, Is.EqualTo(original.EndpointBWorkspace));
            Assert.That(loaded.BackupDirectory, Is.EqualTo(original.BackupDirectory));
            Assert.That(loaded.ConfirmBeforeDelete, Is.False);
            Assert.That(loaded.IllegalCharacterReplacement, Is.EqualTo("-"));
        }

        [Test]
        public void Load_missing_file_returns_defaults()
        {
            var loaded = AppSettingsStore.Load(settingsPath);
            Assert.That(loaded.ConfirmBeforeDelete, Is.True);
            Assert.That(loaded.BackupDirectory, Is.Not.Empty);
        }

        [Test]
        public void Load_migrates_legacy_search_directory_to_endpoint_a()
        {
            var legacy = new EzRealmSyncAppSettings { SearchDirectory = @"D:\legacy\ez" };
            AppSettingsStore.Save(legacy, settingsPath);

            var loaded = AppSettingsStore.Load(settingsPath);

            Assert.That(loaded.EndpointAWorkspace, Is.EqualTo(@"D:\legacy\ez"));
        }
    }
}
