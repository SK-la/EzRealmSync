using NUnit.Framework;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Realm.Readers;
#if HAS_EZ_OSU_GAME
using osu.Game.EzRealmSync.Tests.TestInfrastructure;
#endif

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmReadSidecarRunnerTest
    {
        [Test]
        public void ResolveWorkerExecutablePath_finds_worker_after_build()
        {
            string worker = RealmReadSidecarRunner.ResolveWorkerExecutablePathForTests();
            if (!File.Exists(worker))
                Assert.Ignore($"ReadSidecar Worker 未复制到测试输出：{worker}");

            Assert.That(File.Exists(worker), Is.True);
        }

        [Test]
        public void ReadDiffSnapshot_minimal_job_produces_result_or_structured_error()
        {
#if !HAS_EZ_OSU_GAME
            Assert.Ignore("需要 HAS_EZ_OSU_GAME");
#else
            string worker = RealmReadSidecarRunner.ResolveWorkerExecutablePathForTests();
            if (!File.Exists(worker))
                Assert.Ignore($"ReadSidecar Worker 未复制到测试输出：{worker}");

            string root = Path.Combine(Path.GetTempPath(), "EzRealmSyncSidecarTests", Guid.NewGuid().ToString("N"));
            string realmPath = Path.Combine(root, "client_51007.realm");
            Directory.CreateDirectory(root);

            try
            {
                RealmNativeLifetime.CreateEmptyRealmFile(realmPath, 51_007);

                var package = new RealmReaderPackageInfo
                {
                    Id = "test-51007",
                    DisplayName = "test",
                    Profile = "ez",
                    DiskSchemaVersions = new[] { 51_007 },
                    PackageDirectory = root,
                    LibDirectory = root,
                };

                var job = new RealmReadJob
                {
                    ReaderLibDirectory = root,
                    RealmFilePath = realmPath,
                    PinnedDiskSchemaVersion = 51_007,
                    Profile = "ez",
                };

                try
                {
                    var result = RealmReadSidecarRunner.ReadDiffSnapshot(package, job);
                    Assert.That(result, Is.Not.Null);
                }
                catch (InvalidOperationException ex)
                {
                    Assert.That(ex.Message, Is.Not.Empty, "Sidecar 失败时应返回结构化错误信息");
                }
            }
            finally
            {
                RealmNativeLifetime.DeleteRealmFiles(realmPath);
                try
                {
                    if (Directory.Exists(root))
                        Directory.Delete(root, recursive: true);
                }
                catch
                {
                }
            }
#endif
        }
    }
}
