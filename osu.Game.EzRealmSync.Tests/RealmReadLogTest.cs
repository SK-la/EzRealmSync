using NUnit.Framework;
using osu.Game.EzRealmSync.Realm;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmReadLogTest
    {
        [Test]
        public void DescribeAnomaly_flags_replacement_and_control_characters()
        {
            Assert.That(RealmReadLog.DescribeAnomaly("正常标题"), Is.Null);
            Assert.That(RealmReadLog.DescribeAnomaly("bad\uFFFDtitle"), Is.EqualTo("U+FFFD"));
            Assert.That(RealmReadLog.DescribeAnomaly("a\u0001b"), Is.EqualTo("control"));
            Assert.That(RealmReadLog.DescribeAnomaly("line\nok"), Is.Null);
        }

        [Test]
        public void EscapeSample_uses_code_points_for_non_ascii()
        {
            Assert.That(RealmReadLog.EscapeSample("A\uFFFD"), Is.EqualTo("A\\uFFFD"));
        }
    }
}
