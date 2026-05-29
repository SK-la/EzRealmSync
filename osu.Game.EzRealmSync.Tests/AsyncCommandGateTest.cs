using NUnit.Framework;
using osu.EzRealmSync.AppModel;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class AsyncCommandGateTest
    {
        [Test]
        public void TryEnter_blocks_reentry_until_exit()
        {
            var gate = new AsyncCommandGate();
            Assert.That(gate.TryEnter(), Is.True);
            Assert.That(gate.IsRunning, Is.True);
            Assert.That(gate.TryEnter(), Is.False);

            gate.Exit();
            Assert.That(gate.IsRunning, Is.False);
            Assert.That(gate.TryEnter(), Is.True);
            gate.Exit();
        }

        [Test]
        public void TryEnter_respects_canExecute()
        {
            var gate = new AsyncCommandGate();
            Assert.That(gate.TryEnter(() => false), Is.False);
            Assert.That(gate.IsRunning, Is.False);
        }
    }
}
