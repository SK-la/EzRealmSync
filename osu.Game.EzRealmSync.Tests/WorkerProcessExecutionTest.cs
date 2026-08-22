using NUnit.Framework;
using osu.Game.EzRealmSync.Realm;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class WorkerProcessExecutionTest
    {
        [Test]
        public void CreateWorkerStartInfo_dll_worker_uses_dotnet_exec()
        {
            string workerDll = Path.Combine(TestContext.CurrentContext.WorkDirectory, "read-sidecar", "EzRealmSync.ReadSidecar.dll");
            if (!File.Exists(workerDll))
                Assert.Ignore($"ReadSidecar Worker 未复制到测试输出：{workerDll}");

            var psi = WorkerProcessExecution.CreateWorkerStartInfo(workerDll, new[] { "read", "job.json", "result.json" });

            Assert.That(psi.FileName, Is.EqualTo("dotnet").IgnoreCase);
            Assert.That(psi.ArgumentList, Does.Contain("exec"));
            Assert.That(psi.ArgumentList, Does.Contain(Path.GetFullPath(workerDll)));
            Assert.That(psi.ArgumentList, Does.Contain("read"));
        }

        [Test]
        public void CreateWorkerStartInfo_exe_worker_uses_executable_directly()
        {
            string workerExe = Path.Combine(TestContext.CurrentContext.WorkDirectory, "read-sidecar", "EzRealmSync.ReadSidecar.exe");
            if (!File.Exists(workerExe))
                Assert.Ignore($"ReadSidecar exe 未复制到测试输出：{workerExe}");

            var psi = WorkerProcessExecution.CreateWorkerStartInfo(workerExe, new[] { "read", "job.json" });

            Assert.That(Path.GetFullPath(psi.FileName), Is.EqualTo(Path.GetFullPath(workerExe)));
            Assert.That(psi.ArgumentList, Does.Contain("read"));
        }
    }
}
