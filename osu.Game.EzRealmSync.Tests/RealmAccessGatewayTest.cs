#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Realm.Readers;
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

            Assert.That(ex!.Kind, Is.EqualTo(RealmUserErrorKind.MigrationRequired));
            Assert.That(ex.Message, Does.Contain("Realm 文件").Or.Contain("升级"));
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
            int schema = RealmAccess.EzFileSchemaVersion;
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"gw_ez_current_{Guid.NewGuid():N}.realm");

            try
            {
                RealmNativeLifetime.CreateEmptyRealmFile(path, (ulong)schema);
                Assert.That(
                    RealmAccessGateway.TryOpenInProcessForRead(path, schema, out RealmAccess? access),
                    Is.True);
                Assert.That(access, Is.Not.Null);
                access?.Dispose();
            }
            finally
            {
                RealmNativeLifetime.DeleteRealmFiles(path);
            }
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
        public void RequiresSidecarForRead_true_when_in_process_open_fails()
        {
            var sample = RealmSampleFixture.GetSample("ez-old");
            if (!sample.RealmFileExists)
                Assert.Ignore($"样本未放置 realm 文件：{sample.RealmFilePath}");

            int schema = RealmAccessGateway.ProbeSchema(sample.RealmFilePath) ?? throw new InvalidOperationException("schema 读取失败");
            Assert.That(RealmAccessGateway.RequiresSidecarForRead(sample.RealmFilePath, schema), Is.True);
        }

        [Test]
        public void ReadDiffSnapshot_throws_ReaderPackageMissing_when_no_reader_package()
        {
            string root = Path.Combine(Path.GetTempPath(), "EzRealmSyncGatewayTests", Guid.NewGuid().ToString("N"));
            string path = Path.Combine(root, "client_51007.realm");
            Directory.CreateDirectory(root);

            try
            {
                RealmNativeLifetime.CreateEmptyRealmFile(path, 51_007);
                RealmReaderRegistry.Instance.Initialize(root);

                if (!RealmAccessGateway.RequiresSidecarForRead(path, 51_007))
                    Assert.Ignore("当前 lib 可进程内打开 51007，无法在无 reader 包时触发 ReaderPackageMissing。");

                var ex = Assert.Throws<RealmUserOperationException>((Action)(() =>
                    RealmAccessGateway.ReadDiffSnapshot(path, 51_007)));

                Assert.That(ex!.Kind, Is.EqualTo(RealmUserErrorKind.ReaderPackageMissing));
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

                RealmReaderRegistry.Instance.Refresh();
            }
        }

        [Test]
        public void ReadDiffSnapshot_succeeds_via_sidecar_when_reader_lib_present()
        {
            var sample = RealmSampleFixture.GetSample("ez-old");
            if (!sample.RealmFileExists)
                Assert.Ignore($"样本未放置 realm 文件：{sample.RealmFilePath}");

            int schema = RealmAccessGateway.ProbeSchema(sample.RealmFilePath) ?? throw new InvalidOperationException("schema 读取失败");
            if (!RealmAccessGateway.RequiresSidecarForRead(sample.RealmFilePath, schema))
                Assert.Ignore("当前 lib 可进程内打开该样本，无需 Sidecar。");

            var package = RealmReaderRegistry.Instance.FindPackageForSchema(schema);
            if (package == null || !package.HasValidLib)
                Assert.Ignore($"缺少 schema {schema} 的 reader lib（运行 Sync-ReaderLibs.ps1 后重新 build）。");

            string worker = RealmReadSidecarRunner.ResolveWorkerExecutablePathForTests();
            if (!File.Exists(worker))
                Assert.Ignore($"ReadSidecar Worker 未复制到测试输出：{worker}");

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
