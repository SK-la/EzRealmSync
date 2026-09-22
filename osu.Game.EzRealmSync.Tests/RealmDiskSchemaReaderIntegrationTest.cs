using NUnit.Framework;
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

                Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(path), Is.EqualTo(51_006));
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

                Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(path), Is.EqualTo(51));
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

                int? schema = RealmDiskSchemaReader.TryReadSchemaVersion(path);
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
                Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(path), Is.EqualTo(51_003));
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

            int? schema = RealmDiskSchemaReader.TryReadSchemaVersion(sample.RealmFilePath);
            Assert.That(schema, Is.Not.Null, $"schema 读取失败：{sample.RealmFilePath}");

            bool parsed = Enum.TryParse(sample.DiskSchemaKind, ignoreCase: true, out RealmDiskSchemaKind expectedKind);
            Assert.That(parsed, Is.True, $"manifest expected.diskSchemaKind 非法：{sample.DiskSchemaKind}");
            Assert.That(RealmSchemaSafety.Classify(schema), Is.EqualTo(expectedKind));
        }

        [TestCaseSource(nameof(sample_cases))]
        public void Every_sample_is_readable_dynamically(RealmSampleInfo sample)
        {
            // 动态读取不吃版本号：老 Ez / 官方样本都应能读出，不要求匹配的 DLL，也不改动文件头。
            if (!sample.RealmFileExists)
                Assert.Ignore($"样本未放置 realm 文件：{sample.RealmFilePath}");

            int? schema = RealmDiskSchemaReader.TryReadSchemaVersion(sample.RealmFilePath);
            Assert.That(schema, Is.Not.Null);

            Assert.DoesNotThrow((Action)(() =>
            {
                using var session = RealmAccessGateway.OpenDynamicForRead(sample.RealmFilePath, out _);
            }));

            Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(sample.RealmFilePath), Is.EqualTo(schema), "只读打开改动了文件头。");
        }
    }
}
