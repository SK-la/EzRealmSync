using NUnit.Framework;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class EzRealmSyncLogTest
    {
        [Test]
        public void Initialize_writes_log_entry_to_daily_file()
        {
            EzRealmSyncLog.Initialize();
            EzRealmSyncLog.Info("EzRealmSyncLog test entry");

            string logPath = EzRealmSyncDataPaths.CurrentLogFilePath;
            Assert.That(Directory.Exists(EzRealmSyncDataPaths.LogsDirectory), Is.True);
            Assert.That(File.Exists(logPath), Is.True);
            string text = File.ReadAllText(logPath);
            Assert.That(text, Does.Contain("EzRealmSyncLog test entry"));
            Assert.That(text, Does.Contain("runtime="));
            Assert.That(text, Does.Contain("ansi="));
        }
    }
}
