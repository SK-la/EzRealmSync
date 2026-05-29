using NUnit.Framework;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmSchemaVersionsTest
    {
        [TestCase(51, 0, 51)]
        [TestCase(51, 6, 51_006)]
        [TestCase(51, 3, 51_003)]
        public void EncodeDecode_round_trips(int official, int ez, int combined)
        {
            Assert.That(RealmSchemaVersions.Encode(official, ez), Is.EqualTo(combined));
            Assert.That(RealmSchemaVersions.Decode(combined), Is.EqualTo((official, ez)));
        }

        [Test]
        public void Decode_official_raw_51_is_not_misread_as_ez_51()
        {
            Assert.That(RealmSchemaVersions.Decode(51), Is.EqualTo((51, 0)));
        }

        [Test]
        public void RealmSchemaSafety_distinguishes_official_and_ez()
        {
            Assert.That(RealmSchemaSafety.IsOfficialDiskSchema(51), Is.True);
            Assert.That(RealmSchemaSafety.RequiresOfficialRealmAccess(51), Is.True);
            Assert.That(RealmSchemaSafety.IsEzClientDiskSchema(51), Is.False);

            Assert.That(RealmSchemaSafety.IsEzClientDiskSchema(51_006), Is.True);
            Assert.That(RealmSchemaSafety.RequiresEzRealmAccess(51_006), Is.True);
            Assert.That(RealmSchemaSafety.IsOfficialDiskSchema(51_006), Is.False);
        }
    }
}
