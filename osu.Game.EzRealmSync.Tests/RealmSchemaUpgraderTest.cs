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
        public void UpgradeInPlace_migrates_supported_ez_revision_to_lib_latest()
        {
            int sourceEz = 51 * 1000 + RealmSchemaRevisionCatalog.MinSupportedEzRevision;
            int latest = RealmAccess.EzFileSchemaVersion;
            if (sourceEz >= latest)
                Assert.Ignore("无可用的较低 Ez 修订用于升级测试。");

            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"upgrade_{Guid.NewGuid():N}.realm");

            try
            {
                // 用当前 object schema 写入但标较低同大版本号 —— 仅验证 migration 路径能把版本号抬到最新。
                RealmNativeLifetime.CreateEmptyRealmFile(path, (ulong)sourceEz);

                var result = RealmSchemaUpgrader.UpgradeInPlace(path, sourceEz);

                Assert.That(result.SourceSchemaVersion, Is.EqualTo(sourceEz));
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
            int below = 51 * 1000 + (RealmSchemaRevisionCatalog.MinSupportedEzRevision - 1);

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
