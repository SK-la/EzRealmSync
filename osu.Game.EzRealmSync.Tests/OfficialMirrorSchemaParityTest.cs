#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Realm;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class OfficialMirrorSchemaParityTest
    {
        [Test]
        public void OfficialMirrorWorker_produces_schema_without_ez_columns()
        {
            string worker = OfficialWriteProcessRunner.ResolveWorkerExecutablePathForTests();
            if (!File.Exists(worker))
                Assert.Ignore($"OfficialWrite Worker 未复制到测试输出：{worker}");

            string tempRoot = Path.Combine(Path.GetTempPath(), "ezrealm-mirror-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            string realmPath = Path.Combine(tempRoot, "mirror-test.realm");

            try
            {
                var job = new OfficialConvertJob
                {
                    TargetUpstreamSchema = 51,
                    TargetRealmPath = realmPath,
                    Rulesets =
                    [
                        new OfficialRulesetDto { ShortName = "osu", OnlineID = 0, Name = "osu!" },
                    ],
                    FileHashes = ["abc123"],
                };

                var writeResult = OfficialWriteProcessRunner.Run(job);
                Assert.That(writeResult.Success, Is.True);
                Assert.That(writeResult.RealmFileCount, Is.EqualTo(1));

                OfficialMirrorSchemaVerifier.Verify(realmPath, 51, 1);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void OfficialWriteProcessRunner_locates_worker_after_build()
        {
            string worker = OfficialWriteProcessRunner.ResolveWorkerExecutablePathForTests();
            if (!File.Exists(worker))
                Assert.Ignore($"OfficialWrite Worker 未复制到测试输出：{worker}");

            Assert.That(File.Exists(worker), Is.True);
        }
    }
}
#endif
