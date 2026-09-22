#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Tests.TestInfrastructure;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmAccessGatewayTest
    {
        [Test]
        public void ProbeSchema_reads_disk_header_without_opening_realm()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"gw_probe_{Guid.NewGuid():N}.realm");

            try
            {
                RealmNativeLifetime.CreateEmptyRealmFile(path, 51_006);
                Assert.That(RealmAccessGateway.ProbeSchema(path), Is.EqualTo(51_006));
            }
            finally
            {
                RealmNativeLifetime.DeleteRealmFiles(path);
            }
        }

        [Test]
        public void OpenForMutation_legacy_schema_throws_MigrationRequired_with_sidecar_hint()
        {
            var sample = RealmSampleFixture.GetSample("ez-old");
            if (!sample.RealmFileExists)
                Assert.Ignore($"样本未放置 realm 文件：{sample.RealmFilePath}");

            int schema = RealmAccessGateway.ProbeSchema(sample.RealmFilePath) ?? throw new InvalidOperationException("schema 读取失败");

            var ex = Assert.Throws<RealmUserOperationException>((Action)(() =>
            {
                using var access = RealmAccessGateway.OpenForMutation(sample.RealmFilePath, schema);
                access.Run(_ => { });
            }));

            Assert.That(ex!.Kind, Is.EqualTo(RealmUserErrorKind.MigrationRequired).Or.EqualTo(RealmUserErrorKind.LegacyReaderUnavailable));
            Assert.That(ex.Message, Does.Contain("Realm 文件").Or.Contain("升级").Or.Contain("osu.Game.dll"));
        }

        [Test]
        public void TryOpenInProcessForRead_returns_false_for_official_schema()
        {
            int schema = RealmAccess.UpstreamSchemaVersion;
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"gw_official_{Guid.NewGuid():N}.realm");

            try
            {
                RealmNativeLifetime.CreateEmptyRealmFile(path, (ulong)schema);
                Assert.That(
                    RealmAccessGateway.TryOpenInProcessForRead(path, schema, out RealmAccess? access),
                    Is.False);
                Assert.That(access, Is.Null);
            }
            finally
            {
                RealmNativeLifetime.DeleteRealmFiles(path);
            }
        }

        [Test]
        public void TryOpenInProcessForRead_succeeds_for_current_ez_schema_empty_realm()
        {
            var sample = RealmSampleFixture.GetAllSamples()
                .FirstOrDefault(s => s.CanOpenWithoutMigration && s.RealmFileExists && s.DiskSchemaKind == "EzExtended");
            if (sample == null)
                Assert.Ignore("未放置可进程内打开的当前 Ez 样本。");

            int schema = RealmAccessGateway.ProbeSchema(sample.RealmFilePath) ?? throw new InvalidOperationException("schema 读取失败");
            Assert.That(
                RealmAccessGateway.TryOpenInProcessForRead(sample.RealmFilePath, schema, out RealmAccess? access),
                Is.True);
            Assert.That(access, Is.Not.Null);
            access?.Dispose();
        }

        [Test]
        public void TryOpenInProcessForRead_returns_false_without_throw_when_legacy_open_fails()
        {
            var sample = RealmSampleFixture.GetSample("ez-old");
            if (!sample.RealmFileExists)
                Assert.Ignore($"样本未放置 realm 文件：{sample.RealmFilePath}");

            int schema = RealmAccessGateway.ProbeSchema(sample.RealmFilePath) ?? throw new InvalidOperationException("schema 读取失败");

            Assert.That(
                RealmAccessGateway.TryOpenInProcessForRead(sample.RealmFilePath, schema, out RealmAccess? access),
                Is.False);
            Assert.That(access, Is.Null);
        }

        [Test]
        public void ReadDiffSnapshot_uses_dynamic_baseline_without_reader_package()
        {
            string root = Path.Combine(Path.GetTempPath(), "EzRealmSyncGatewayTests", Guid.NewGuid().ToString("N"));
            string path = Path.Combine(root, "client_51007.realm");
            Directory.CreateDirectory(root);

            try
            {
                RealmNativeLifetime.CreateEmptyRealmFile(path, 51_007);
                Assert.DoesNotThrow((Action)(() => RealmAccessGateway.ReadDiffSnapshot(path, 51_007)));
            }
            finally
            {
                RealmNativeLifetime.DeleteRealmFiles(path);
                try
                {
                    if (Directory.Exists(root))
                        Directory.Delete(root, recursive: true);
                }
                catch
                {
                }
            }
        }

        [Test]
        public void ReadDiffSnapshot_reads_legacy_sample_without_sidecar()
        {
            var sample = RealmSampleFixture.GetSample("ez-old");
            if (!sample.RealmFileExists)
                Assert.Ignore($"样本未放置 realm 文件：{sample.RealmFilePath}");

            int schema = RealmAccessGateway.ProbeSchema(sample.RealmFilePath) ?? throw new InvalidOperationException("schema 读取失败");
            Assert.DoesNotThrow((Action)(() => RealmAccessGateway.ReadDiffSnapshot(sample.RealmFilePath, schema)));
        }

        [Test]
        public void Production_services_do_not_call_RealmSchemaProbe_Open_directly()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));
            string projectDir = Path.Combine(repoRoot, "osu.Game.EzRealmSync");
            Assert.That(Directory.Exists(projectDir), Is.True, projectDir);

            var offenders = Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                .Where(path => !string.Equals(Path.GetFileName(path), "RealmAccessGateway.cs", StringComparison.OrdinalIgnoreCase))
                .Where(path => !string.Equals(Path.GetFileName(path), "RealmSchemaProbe.cs", StringComparison.OrdinalIgnoreCase))
                .Where(path => File.ReadAllText(path).Contains("RealmSchemaProbe.Open", StringComparison.Ordinal))
                .Select(p => Path.GetRelativePath(repoRoot, p))
                .ToList();

            Assert.That(offenders, Is.Empty, $"以下文件仍直接调用 RealmSchemaProbe.Open：{string.Join(", ", offenders)}");
        }

        [Test]
        public void LoadRealmSnapshot_does_not_call_OpenForMutation()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));
            string path = Path.Combine(repoRoot, "osu.Game.EzRealmSync", "Realm", "RealmRealmDataService.cs");
            Assert.That(File.ReadAllText(path), Does.Not.Contain("OpenForMutation"));
        }
    }
}
#endif
