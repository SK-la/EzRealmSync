using NUnit.Framework;
using osu.Game.EzRealmSync.IO;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class LegacyScoresDbTest
    {
        [Test]
        public void Write_then_Read_roundtrips_groups()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"scores_{Guid.NewGuid():N}.db");

            try
            {
                var groups = new[]
                {
                    new LegacyScoresDbBeatmapGroup("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", new[]
                    {
                        new LegacyScoresDbScore
                        {
                            GameplayMode = 0,
                            Version = LegacyScoresDb.DefaultVersion,
                            BeatmapMd5 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                            PlayerName = "player",
                            ReplayMd5 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                            Count300 = 300,
                            Count100 = 10,
                            CountMiss = 1,
                            TotalScore = 1234567,
                            MaxCombo = 400,
                            PerfectCombo = false,
                            Mods = 8,
                            TimestampTicks = 638000000000000000,
                            OnlineScoreId = 42,
                        },
                    }),
                };

                LegacyScoresDb.WriteFile(path, groups);
                var read = LegacyScoresDb.ReadFile(path);

                Assert.That(read, Has.Count.EqualTo(1));
                Assert.That(read[0].BeatmapMd5, Is.EqualTo("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
                Assert.That(read[0].Scores, Has.Count.EqualTo(1));
                Assert.That(read[0].Scores[0].PlayerName, Is.EqualTo("player"));
                Assert.That(read[0].Scores[0].TotalScore, Is.EqualTo(1234567));
                Assert.That(read[0].Scores[0].OnlineScoreId, Is.EqualTo(42));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void IsScoresDbFileName_matches_stable_name()
        {
            Assert.That(LegacyScoresDb.IsScoresDbFileName("scores.db"), Is.True);
            Assert.That(LegacyScoresDb.IsScoresDbFileName("collection.db"), Is.False);
        }
    }
}
