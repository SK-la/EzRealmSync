#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Tests.TestInfrastructure;

namespace osu.Game.EzRealmSync.Tests
{
    /// <summary>
    /// 版本号只用于识别/展示：既不再决定"能不能开"，也不再决定"按哪套模型开"。
    /// 动态打开不传 schema，realm-core 因此没有按版本迁移的路径（见 <see cref="DynamicRealmSession"/>）。
    /// </summary>
    [TestFixture]
    public class RealmSchemaToolPolicyTest
    {
        [Test]
        public void Min_is_constant_max_is_lib()
        {
            Assert.That(RealmSchemaToolPolicy.MinSupportedOfficialSchema, Is.EqualTo(RealmSchemaRevisionCatalog.MinSupportedOfficialUpstream));
            Assert.That(RealmSchemaToolPolicy.MaxSupportedOfficialSchema, Is.EqualTo(RealmAccess.UpstreamSchemaVersion));
            Assert.That(RealmSchemaToolPolicy.MaxSupportedEzFileSchema, Is.EqualTo(RealmAccess.EzFileSchemaVersion));
        }

        /// <summary>
        /// 白名单之外的版本（低于最低官方 50、高于内置 lib、Ez 修订低于最低、Ez 形式的 52010）
        /// 都必须能动态打开并如实读回版本号，不得抛 SchemaTooHigh / SchemaTooLow。
        /// </summary>
        [TestCase(49)]
        [TestCase(53)]
        [TestCase(51_002)]
        [TestCase(52_010)]
        public void Dynamic_open_does_not_gate_on_schema_version(int diskSchemaVersion)
        {
            string root = EzRealmSyncDataPaths.CreateTempSubdirectory("schema-gate");
            string path = Path.Combine(root, $"{diskSchemaVersion}.realm");

            try
            {
                RealmNativeLifetime.CreateEmptyRealmFile(path, (ulong)diskSchemaVersion);

                using var session = DynamicRealmSession.OpenDynamic(path, readOnly: true);

                Assert.Multiple(() =>
                {
                    Assert.That(session.DiskSchemaVersion, Is.EqualTo(diskSchemaVersion));
                    Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(path), Is.EqualTo(diskSchemaVersion));
                });
            }
            finally
            {
                RealmNativeLifetime.DeleteRealmFiles(path);

                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }
    }
}
#endif
