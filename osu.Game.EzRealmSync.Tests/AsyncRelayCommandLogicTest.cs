using NUnit.Framework;
using osu.EzRealmSync.AppModel;

namespace osu.Game.EzRealmSync.Tests
{
    /// <summary>
    /// 验证 AsyncRelayCommand 所依赖的门闩 + SafeAsyncInvoker 组合行为（不引用 WPF）。
    /// </summary>
    [TestFixture]
    public class AsyncRelayCommandLogicTest
    {
        [Test]
        public async Task Gate_and_safeInvoker_match_async_command_lifecycle()
        {
            var gate = new AsyncCommandGate();
            var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Exception? caught = null;

            Assert.That(gate.TryEnter(), Is.True);
            SafeAsyncInvoker.Run(async () =>
            {
                try
                {
                    await Task.Yield();
                    completed.TrySetResult();
                }
                finally
                {
                    gate.Exit();
                }
            }, ex =>
            {
                gate.Exit();
                caught = ex;
            });

            await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(caught, Is.Null);
            Assert.That(gate.IsRunning, Is.False);
        }

        [Test]
        public async Task SafeInvoker_resets_gate_when_work_throws()
        {
            var gate = new AsyncCommandGate();
            var errored = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            Assert.That(gate.TryEnter(), Is.True);
            SafeAsyncInvoker.Run(async () =>
            {
                await Task.Yield();
                throw new InvalidOperationException("cmd");
            }, _ =>
            {
                gate.Exit();
                errored.TrySetResult();
            });

            await errored.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(gate.IsRunning, Is.False);
        }
    }
}
