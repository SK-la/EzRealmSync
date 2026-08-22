#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;
using Realms;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmSchemaUpgraderTest
    {
        [Test]
        public void UpgradeInPlace_migrates_same_major_ez_revision_to_latest()
        {
            int minEz = RealmSchemaToolPolicy.MinSupportedEzFileSchema;
            int latest = RealmAccess.EzFileSchemaVersion;
            if (minEz >= latest)
                Assert.Ignore("当前 Ez 仅一个修订，无法测同大版本内升级。");

            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"upgrade_{Guid.NewGuid():N}.realm");

            try
            {
                // 用当前 object schema 写入但标较低同大版本号 —— 仅验证 migration 路径能把版本号抬到最新。
                var writeConfig = new RealmConfiguration(path) { SchemaVersion = (ulong)minEz };
                using (RealmInstance.GetInstance(writeConfig))
                {
                }

                var result = RealmSchemaUpgrader.UpgradeInPlace(path, minEz);

                Assert.That(result.SourceSchemaVersion, Is.EqualTo(minEz));
                Assert.That(result.TargetSchemaVersion, Is.EqualTo(latest));
                Assert.That(result.AlreadyUpToDate, Is.False);

                int? diskSchema = RealmDiskSchemaReader.TryReadSchemaVersion(path);
                Assert.That(diskSchema, Is.EqualTo(latest));
            }
            finally
            {
                deleteRealm(path);
            }
        }

        [Test]
        public void UpgradeInPlace_reports_already_latest_for_current_schema()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"latest_{Guid.NewGuid():N}.realm");

            try
            {
                int latest = RealmAccess.EzFileSchemaVersion;
                var writeConfig = new RealmConfiguration(path) { SchemaVersion = (ulong)latest };
                using (RealmInstance.GetInstance(writeConfig))
                {
                }

                var result = RealmSchemaUpgrader.UpgradeInPlace(path, latest);

                Assert.That(result.AlreadyUpToDate, Is.True);
                Assert.That(result.TargetSchemaVersion, Is.EqualTo(latest));
            }
            finally
            {
                deleteRealm(path);
            }
        }

        [Test]
        public void UpgradeInPlace_rejects_below_min_supported()
        {
            int below = RealmSchemaToolPolicy.MinSupportedEzFileSchema - 1;
            if (below < 1000)
                Assert.Ignore("无法构造低于最低支持的 Ez schema。");

            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"too_low_{Guid.NewGuid():N}.realm");

            try
            {
                var writeConfig = new RealmConfiguration(path) { SchemaVersion = (ulong)below };
                using (RealmInstance.GetInstance(writeConfig))
                {
                }

                var ex = Assert.Throws<RealmUserOperationException>(() => RealmSchemaUpgrader.UpgradeInPlace(path, below));
                Assert.That(ex!.Kind, Is.EqualTo(RealmUserErrorKind.SchemaTooLow));
            }
            finally
            {
                deleteRealm(path);
            }
        }

        private static void deleteRealm(string path)
        {
            if (File.Exists(path))
                File.Delete(path);

            string lockPath = path + ".lock";
            if (File.Exists(lockPath))
                File.Delete(lockPath);
        }
    }
}
#endif
