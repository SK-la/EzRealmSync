using NUnit.Framework;
using osu.Game.EzRealmSync.IO;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmBackupCatalogTest
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
        public void List_returns_timestamped_backups_newest_first()
        {
            Directory.CreateDirectory(tempRoot);
            string older = Path.Combine(tempRoot, "client_20260101_120000.realm");
            string newer = Path.Combine(tempRoot, "client_20260201_120000.realm");
            File.WriteAllText(older, "old");
            File.WriteAllText(newer, "new");
            File.SetCreationTimeUtc(older, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
            File.SetCreationTimeUtc(newer, new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc));

            var entries = RealmBackupCatalog.List(tempRoot);

            Assert.That(entries, Has.Count.EqualTo(2));
            Assert.That(entries[0].Path, Is.EqualTo(newer));
            Assert.That(RealmBackupCatalog.TryInferOriginalFileName(Path.GetFileName(newer), out string original), Is.True);
            Assert.That(original, Is.EqualTo("client.realm"));
        }

        [Test]
        public void TryFind_locates_entry_by_id()
        {
            Directory.CreateDirectory(tempRoot);
            string backup = Path.Combine(tempRoot, "client_20260529_120000.realm");
            File.WriteAllText(backup, "payload");

            var listed = RealmBackupCatalog.List(tempRoot).Single();

            Assert.That(RealmBackupCatalog.TryFind(tempRoot, listed.Id, out var found), Is.True);
            Assert.That(found.Path, Is.EqualTo(backup));
        }

        [Test]
        public void CreateEntryId_is_stable_for_same_path()
        {
            string path = Path.Combine(tempRoot, "client_20260529_120000.realm");
            Assert.That(RealmBackupCatalog.CreateEntryId(path), Is.EqualTo(RealmBackupCatalog.CreateEntryId(path)));
        }
    }
}
