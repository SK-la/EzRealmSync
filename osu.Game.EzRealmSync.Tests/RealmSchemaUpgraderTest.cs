#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.EzRealmSync.Realm;
using Realms;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmSchemaUpgraderTest
    {
        [Test]
        public void UpgradeInPlace_migrates_legacy_ez_schema_to_latest()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"upgrade_{Guid.NewGuid():N}.realm");

            try
            {
                var writeConfig = new RealmConfiguration(path) { SchemaVersion = 51_003 };
                using (RealmInstance.GetInstance(writeConfig))
                {
                }

                var result = RealmSchemaUpgrader.UpgradeInPlace(path, 51_003);

                Assert.That(result.SourceSchemaVersion, Is.EqualTo(51_003));
                Assert.That(result.TargetSchemaVersion, Is.EqualTo(RealmAccess.EzFileSchemaVersion));
                Assert.That(result.AlreadyUpToDate, Is.False);

                int? diskSchema = RealmDiskSchemaReader.TryReadSchemaVersion(path);
                Assert.That(diskSchema, Is.EqualTo(RealmAccess.EzFileSchemaVersion));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);

                string lockPath = path + ".lock";
                if (File.Exists(lockPath))
                    File.Delete(lockPath);
            }
        }

        [Test]
        public void UpgradeInPlace_reports_already_latest_for_current_schema()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"latest_{Guid.NewGuid():N}.realm");

            try
            {
                int latest = RealmAccess.EzFileSchemaVersion;
                var writeConfig = new RealmConfiguration(path) { SchemaVersion = (uint)latest };
                using (RealmInstance.GetInstance(writeConfig))
                {
                }

                var result = RealmSchemaUpgrader.UpgradeInPlace(path, latest);

                Assert.That(result.AlreadyUpToDate, Is.True);
                Assert.That(result.TargetSchemaVersion, Is.EqualTo(latest));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);

                string lockPath = path + ".lock";
                if (File.Exists(lockPath))
                    File.Delete(lockPath);
            }
        }
    }
}
#endif
