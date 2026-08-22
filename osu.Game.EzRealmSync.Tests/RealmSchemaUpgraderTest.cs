#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Tests.TestInfrastructure;

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
                RealmNativeLifetime.CreateEmptyRealmFile(path, (ulong)minEz);

                var result = RealmSchemaUpgrader.UpgradeInPlace(path, minEz);

                Assert.That(result.SourceSchemaVersion, Is.EqualTo(minEz));
                Assert.That(result.TargetSchemaVersion, Is.EqualTo(latest));
                Assert.That(result.AlreadyUpToDate, Is.False);

                int? diskSchema = RealmDiskSchemaReader.TryReadSchemaVersion(path);
                Assert.That(diskSchema, Is.EqualTo(latest));
            }
            finally
            {
                RealmNativeLifetime.DeleteRealmFiles(path);
            }
        }

        [Test]
        public void UpgradeInPlace_reports_already_latest_for_current_schema()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"latest_{Guid.NewGuid():N}.realm");

            try
            {
                int latest = RealmAccess.EzFileSchemaVersion;
                RealmNativeLifetime.CreateEmptyRealmFile(path, (ulong)latest);

                var result = RealmSchemaUpgrader.UpgradeInPlace(path, latest);

                Assert.That(result.AlreadyUpToDate, Is.True);
                Assert.That(result.TargetSchemaVersion, Is.EqualTo(latest));
            }
            finally
            {
                RealmNativeLifetime.DeleteRealmFiles(path);
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
                RealmNativeLifetime.CreateEmptyRealmFile(path, (ulong)below);

                var ex = Assert.Throws<RealmUserOperationException>((Action)(() =>
                    RealmSchemaUpgrader.UpgradeInPlace(path, below)));
                Assert.That(ex!.Kind, Is.EqualTo(RealmUserErrorKind.SchemaTooLow));
            }
            finally
            {
                RealmNativeLifetime.DeleteRealmFiles(path);
            }
        }
    }
}
#endif
