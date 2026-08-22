#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmSchemaTransitionAssessorTest
    {
        [Test]
        public void HasUpstreamMismatch_when_official_parts_differ()
        {
            Assert.That(RealmSchemaTransitionAssessor.HasUpstreamMismatch(51_006, 52_007), Is.True);
            Assert.That(RealmSchemaTransitionAssessor.HasUpstreamMismatch(51_003, 51_006), Is.False);
        }

        [Test]
        public void DescribeSyncPairWarning_notes_upstream_mismatch()
        {
            string warning = RealmSchemaTransitionAssessor.DescribeSyncPairWarning(51_006, 52_007);
            Assert.That(warning, Does.Contain("51"));
            Assert.That(warning, Does.Contain("52"));
        }
    }
}
#endif
