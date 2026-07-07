using NUnit.Framework;
using osu.EzRealmSync.AppModel;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class SafeAsyncInvokerTest
    {
        [Test]
        public void Run_does_not_throw_synchronously_when_work_fails()
        {
            Assert.DoesNotThrow((Action)(() => SafeAsyncInvoker.Run(() => throw new InvalidOperationException("boom"))));
        }

        [Test]
        public async Task Run_invokes_onError_when_work_fails()
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Exception? caught = null;

            SafeAsyncInvoker.Run(async () =>
            {
                await Task.Yield();
                throw new InvalidOperationException("boom");
            }, ex =>
            {
                caught = ex;
                gate.TrySetResult();
            });

            await gate.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(caught, Is.InstanceOf<InvalidOperationException>());
            Assert.That(caught!.Message, Is.EqualTo("boom"));
        }

        [Test]
        public async Task Run_invokes_default_handler_when_onError_omitted()
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Exception? caught = null;
            SafeAsyncInvoker.DefaultExceptionHandler = ex =>
            {
                caught = ex;
                gate.TrySetResult();
            };

            try
            {
                SafeAsyncInvoker.Run(() => throw new InvalidOperationException("default"));
                await gate.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.That(caught, Is.InstanceOf<InvalidOperationException>());
            }
            finally
            {
                SafeAsyncInvoker.DefaultExceptionHandler = null;
            }
        }

        [Test]
        public async Task Run_completes_successfully_without_handler()
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            SafeAsyncInvoker.Run(async () =>
            {
                await Task.Yield();
                gate.TrySetResult();
            });

            await gate.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
