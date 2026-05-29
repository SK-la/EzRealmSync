using NUnit.Framework;
using osu.Game.EzRealmSync.IO;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmFileBackupTest
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
                // 临时目录可能被占用，忽略清理失败。
            }
        }

        [Test]
        public void CreateTimestampedCopy_copies_without_modifying_source()
        {
            Directory.CreateDirectory(tempRoot);
            string backupDir = Path.Combine(tempRoot, "backups");
            string source = Path.Combine(tempRoot, "client.realm");
            byte[] payload = { 1, 2, 3, 4, 5 };
            File.WriteAllBytes(source, payload);
            DateTimeOffset stamp = new DateTimeOffset(2026, 5, 29, 12, 0, 0, TimeSpan.Zero);

            string backup = RealmFileBackup.CreateTimestampedCopy(source, backupDir, stamp);

            Assert.That(File.Exists(backup), Is.True);
            Assert.That(File.ReadAllBytes(source), Is.EqualTo(payload));
            Assert.That(File.ReadAllBytes(backup), Is.EqualTo(payload));
            Assert.That(Path.GetFileName(backup), Is.EqualTo("client_20260529_120000.realm"));
        }

        [Test]
        public void CreateTimestampedCopy_does_not_overwrite_existing_backup()
        {
            Directory.CreateDirectory(tempRoot);
            string backupDir = Path.Combine(tempRoot, "backups");
            string source = Path.Combine(tempRoot, "client.realm");
            File.WriteAllText(source, "original");
            var stamp = new DateTimeOffset(2026, 5, 29, 12, 0, 0, TimeSpan.Zero);

            RealmFileBackup.CreateTimestampedCopy(source, backupDir, stamp);

            Assert.Throws<IOException>(() => RealmFileBackup.CreateTimestampedCopy(source, backupDir, stamp));
            Assert.That(File.ReadAllText(source), Is.EqualTo("original"));
        }

        [Test]
        public void CreateTimestampedCopy_throws_when_source_missing()
        {
            string backupDir = Path.Combine(tempRoot, "backups");
            string source = Path.Combine(tempRoot, "missing.realm");

            Assert.Throws<FileNotFoundException>(() => RealmFileBackup.CreateTimestampedCopy(source, backupDir));
        }
    }
}
