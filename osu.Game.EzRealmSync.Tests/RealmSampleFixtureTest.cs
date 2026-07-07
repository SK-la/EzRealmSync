using NUnit.Framework;
using osu.Game.EzRealmSync.Tests.TestInfrastructure;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmSampleFixtureTest
    {
        [Test]
        public void GetAllSamples_contains_three_expected_kinds()
        {
            var samples = RealmSampleFixture.GetAllSamples();
            var kinds = samples.Select(s => s.Kind).ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.That(kinds, Does.Contain("official"));
            Assert.That(kinds, Does.Contain("ez-old"));
            Assert.That(kinds, Does.Contain("ez-new"));
        }

        [Test]
        public void GetSample_parses_manifest_fields()
        {
            var sample = RealmSampleFixture.GetSample("official");

            Assert.That(sample.Kind, Is.EqualTo("official"));
            Assert.That(sample.ManifestPath, Does.EndWith("manifest.json"));
            Assert.That(sample.RealmFileName, Is.EqualTo("client.realm"));
            Assert.That(sample.DiskSchemaKind, Is.Not.Empty);
        }
    }
}
