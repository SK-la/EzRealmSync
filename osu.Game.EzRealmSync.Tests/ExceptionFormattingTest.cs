using NUnit.Framework;
using osu.Game.EzRealmSync.Contracts;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class ExceptionFormattingTest
    {
        [Test]
        public void SafeFormat_includes_type_and_message()
        {
            var ex = new InvalidOperationException("boom");
            string formatted = ExceptionFormatting.SafeFormat(ex);

            Assert.That(formatted, Does.Contain(nameof(InvalidOperationException)));
            Assert.That(formatted, Does.Contain("boom"));
        }

        [Test]
        public void SafeFormat_does_not_call_ToString_on_throw_toString_exception()
        {
            var ex = new ThrowingToStringException("inner message");
            string formatted = ExceptionFormatting.SafeFormat(ex);

            Assert.That(formatted, Does.Contain("inner message"));
            Assert.That(formatted, Does.Contain(nameof(ThrowingToStringException)));
        }

        [Test]
        public void TruncateForDisplay_shortens_long_text()
        {
            string text = new string('x', 600);
            string truncated = ExceptionFormatting.TruncateForDisplay(text, 500);

            Assert.That(truncated.Length, Is.EqualTo(501));
            Assert.That(truncated, Does.EndWith("…"));
        }

        private sealed class ThrowingToStringException : Exception
        {
            public ThrowingToStringException(string message)
                : base(message)
            {
            }

            public override string ToString() => throw new InvalidOperationException("ToString must not be called");
        }
    }
}
