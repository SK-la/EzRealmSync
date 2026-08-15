using NUnit.Framework;
using osu.Game.EzRealmSync.IO;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class LegacyCollectionDbTest
    {
        private string tempRoot = null!;

        [SetUp]
        public void SetUp() => tempRoot = Path.Combine(Path.GetTempPath(), "EzRealmSyncTests", Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
            }
        }

        [Test]
        public void Roundtrip_preserves_names_and_md5_hashes()
        {
            var original = new[]
            {
                new LegacyCollectionDbEntry("First", new[] { "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" }),
                new LegacyCollectionDbEntry("Second", new[]
                {
                    "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                    "cccccccccccccccccccccccccccccccc",
                }),
                new LegacyCollectionDbEntry("Empty", Array.Empty<string>()),
            };

            using var stream = new MemoryStream();
            LegacyCollectionDb.Write(stream, original);
            stream.Position = 0;

            var read = LegacyCollectionDb.Read(stream);
            Assert.That(read, Has.Count.EqualTo(3));
            Assert.That(read[0].Name, Is.EqualTo("First"));
            Assert.That(read[0].BeatmapMd5Hashes, Is.EqualTo(original[0].BeatmapMd5Hashes));
            Assert.That(read[1].Name, Is.EqualTo("Second"));
            Assert.That(read[1].BeatmapMd5Hashes, Is.EqualTo(original[1].BeatmapMd5Hashes));
            Assert.That(read[2].Name, Is.EqualTo("Empty"));
            Assert.That(read[2].BeatmapMd5Hashes, Is.Empty);
        }

        [Test]
        public void Read_empty_stream_returns_no_collections()
        {
            using var stream = new MemoryStream();
            Assert.That(LegacyCollectionDb.Read(stream), Is.Empty);
        }

        [Test]
        public void File_names_accept_stable_and_community_aliases()
        {
            Assert.That(LegacyCollectionDb.IsCollectionDbFileName("collection.db"), Is.True);
            Assert.That(LegacyCollectionDb.IsCollectionDbFileName(@"C:\osu!\collections.db"), Is.True);
            Assert.That(LegacyCollectionDb.IsCollectionDbFileName("scores.db"), Is.False);
        }

        [Test]
        public void ResolveOutputFile_uses_stable_filename_inside_folder()
        {
            Directory.CreateDirectory(tempRoot);
            string path = LegacyCollectionDb.ResolveOutputFile(tempRoot, "my-pack");
            Assert.That(path, Is.EqualTo(Path.Combine(tempRoot, "my-pack", LegacyCollectionDb.StableFileName)));
        }

        [Test]
        public void ResolveOutputFile_writes_db_name_directly()
        {
            string path = LegacyCollectionDb.ResolveOutputFile(tempRoot, "collections.db");
            Assert.That(path, Is.EqualTo(Path.Combine(tempRoot, "collections.db")));
        }

        [Test]
        public void WriteFile_then_ReadFile_roundtrips()
        {
            Directory.CreateDirectory(tempRoot);
            string path = Path.Combine(tempRoot, LegacyCollectionDb.AlternateFileName);
            var original = new[] { new LegacyCollectionDbEntry("Shared", new[] { "dddddddddddddddddddddddddddddddd" }) };

            LegacyCollectionDb.WriteFile(path, original);
            var read = LegacyCollectionDb.ReadFile(path);

            Assert.That(read, Has.Count.EqualTo(1));
            Assert.That(read[0].Name, Is.EqualTo("Shared"));
            Assert.That(read[0].BeatmapMd5Hashes[0], Is.EqualTo("dddddddddddddddddddddddddddddddd"));
        }
    }
}
