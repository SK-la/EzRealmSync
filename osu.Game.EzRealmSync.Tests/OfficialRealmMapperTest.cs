#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzRealmSync.Realm;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class OfficialRealmMapperTest
    {
        [Test]
        public void NormalizeEzOnlyBeatmapFields_resets_to_pending_sentinels()
        {
            var beatmap = new BeatmapInfo
            {
                XxyStarRating = 8.5,
                PerformancePoints = 420,
                HasVideo = true,
                HasStoryboard = false,
            };

            OfficialRealmMapper.NormalizeEzOnlyBeatmapFields(beatmap);

            Assert.That(beatmap.XxyStarRating, Is.EqualTo(-1));
            Assert.That(beatmap.PerformancePoints, Is.EqualTo(-1));
            Assert.That(beatmap.HasVideo, Is.Null);
            Assert.That(beatmap.HasStoryboard, Is.Null);
        }

        [Test]
        public void StripEzOnlyBeatmapFields_matches_normalize_sentinels()
        {
            var beatmap = new BeatmapInfo
            {
                XxyStarRating = 3,
                PerformancePoints = 100,
                HasVideo = false,
                HasStoryboard = true,
            };

            OfficialRealmMapper.StripEzOnlyBeatmapFields(beatmap);

            Assert.That(beatmap.XxyStarRating, Is.EqualTo(-1));
            Assert.That(beatmap.PerformancePoints, Is.EqualTo(-1));
            Assert.That(beatmap.HasVideo, Is.Null);
            Assert.That(beatmap.HasStoryboard, Is.Null);
        }
    }
}
#endif
