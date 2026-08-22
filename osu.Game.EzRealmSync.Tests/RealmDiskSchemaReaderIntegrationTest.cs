#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Tests.TestInfrastructure;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmDiskSchemaReaderIntegrationTest
    {
        private static IEnumerable<TestCaseData> sample_cases() =>
            RealmSampleFixture.GetAllSamples().Select(sample => new TestCaseData(sample).SetName($"sample_schema_{sample.Kind}"));

        [Test]
        public void TryReadSchemaVersion_reads_version_from_created_realm()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"probe_{Guid.NewGuid():N}.realm");

            try
            {
                RealmNativeLifetime.CreateEmptyRealmFile(path, 51_006);

                Assert.That(RealmSchemaProbe.TryReadSchemaVersion(path), Is.EqualTo(51_006));
            }
            finally
            {
                RealmNativeLifetime.DeleteRealmFiles(path);
            }
        }

        [Test]
        public void TryReadSchemaVersion_reads_official_upstream_schema()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"probe_official_{Guid.NewGuid():N}.realm");

            try
            {
                RealmNativeLifetime.CreateEmptyRealmFile(path, 51);

                Assert.That(RealmSchemaProbe.TryReadSchemaVersion(path), Is.EqualTo(51));
            }
            finally
            {
                RealmNativeLifetime.DeleteRealmFiles(path);
            }
        }

        [Test]
        public void TryReadSchemaVersion_plain_client_realm_at_51_does_not_report_ez_suffix()
        {
            string dir = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"probe_client_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "client.realm");

            try
            {
                RealmNativeLifetime.CreateEmptyRealmFile(path, 51);

                int? schema = RealmSchemaProbe.TryReadSchemaVersion(path);
                Assert.That(schema, Is.EqualTo(51));
                Assert.That(RealmSchemaSafety.Classify(schema), Is.EqualTo(RealmDiskSchemaKind.PpyClient));
            }
            finally
            {
                RealmNativeLifetime.Flush();
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }

        [Test]
        public void TryReadSchemaVersion_infers_from_versioned_filename_when_open_fails()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "client_51003.realm");
            File.WriteAllBytes(path, Array.Empty<byte>());

            try
            {
                Assert.That(RealmSchemaProbe.TryReadSchemaVersion(path), Is.EqualTo(51_003));
            }
            finally
            {
                RealmNativeLifetime.DeleteRealmFiles(path);
            }
        }

        [TestCaseSource(nameof(sample_cases))]
        public void TryReadSchemaVersion_matches_manifest_kind(RealmSampleInfo sample)
        {
            if (!sample.RealmFileExists)
                Assert.Ignore($"样本未放置 realm 文件：{sample.RealmFilePath}");

            int? schema = RealmSchemaProbe.TryReadSchemaVersion(sample.RealmFilePath);
            Assert.That(schema, Is.Not.Null, $"schema 读取失败：{sample.RealmFilePath}");

            bool parsed = Enum.TryParse(sample.DiskSchemaKind, ignoreCase: true, out RealmDiskSchemaKind expectedKind);
            Assert.That(parsed, Is.True, $"manifest expected.diskSchemaKind 非法：{sample.DiskSchemaKind}");
            Assert.That(RealmSchemaSafety.Classify(schema), Is.EqualTo(expectedKind));
        }

        [TestCaseSource(nameof(sample_cases))]
        public void Open_behaviour_matches_manifest_expectation(RealmSampleInfo sample)
        {
            if (!sample.RealmFileExists)
                Assert.Ignore($"样本未放置 realm 文件：{sample.RealmFilePath}");

            bool parsed = Enum.TryParse(sample.DiskSchemaKind, ignoreCase: true, out RealmDiskSchemaKind expectedKind);
            Assert.That(parsed, Is.True, $"manifest expected.diskSchemaKind 非法：{sample.DiskSchemaKind}");

            if (sample.CanOpenWithoutMigration)
            {
                Assert.DoesNotThrow((Action)(() =>
                {
                    using var access = RealmSchemaProbe.Open(sample.RealmFilePath);
                    if (expectedKind == RealmDiskSchemaKind.PpyClient)
                        Assert.That(access.GetType().Name, Is.EqualTo("OfficialRealmAccess"));
                    else if (expectedKind == RealmDiskSchemaKind.EzExtended)
                        Assert.That(access.GetType().Name, Is.Not.EqualTo("OfficialRealmAccess"));
                    access.Run(_ => { });
                }));
                return;
            }

            var ex = Assert.Throws<RealmUserOperationException>((Action)(() =>
            {
                using var access = RealmSchemaProbe.Open(sample.RealmFilePath);
                access.Run(_ => { });
            }));
            Assert.That(ex!.Kind, Is.AnyOf(RealmUserErrorKind.MigrationRequired, RealmUserErrorKind.SchemaTooLow));
        }
    }
}
#endif
