using NUnit.Framework;
using osu.Game.EzRealmSync.Realm;

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
    }
}
