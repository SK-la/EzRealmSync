using NUnit.Framework;
using osu.Game.EzRealmSync.Realm.Readers;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmReaderPackageCatalogTest
    {
        private string root = null!;

        [SetUp]
        public void SetUp() => root = Path.Combine(Path.GetTempPath(), "EzRealmSyncReaderTests", Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }

        [Test]
        public void Scan_finds_valid_manifest()
        {
            string packageDir = Path.Combine(root, "ez-51003");
            string libDir = Path.Combine(packageDir, "lib");
            Directory.CreateDirectory(libDir);
            File.WriteAllText(Path.Combine(packageDir, "manifest.json"), """
                {
                  "id": "ez-51003",
                  "displayName": "Ez 51003",
                  "profile": "ez",
                  "diskSchemaVersions": [51003]
                }
                """);
            File.WriteAllText(Path.Combine(libDir, "osu.Game.dll"), string.Empty);

            var packages = RealmReaderPackageCatalog.Scan(root);

            Assert.That(packages, Has.Count.EqualTo(1));
            Assert.That(packages[0].Id, Is.EqualTo("ez-51003"));
            Assert.That(packages[0].Supports(51003), Is.True);
            Assert.That(packages[0].HasValidLib, Is.True);
        }

        [Test]
        public void FindForSchema_returns_package_with_matching_schema_and_lib()
        {
            string packageDir = Path.Combine(root, "ez-51003");
            string libDir = Path.Combine(packageDir, "lib");
            Directory.CreateDirectory(libDir);
            File.WriteAllText(Path.Combine(packageDir, "manifest.json"), """
                {
                  "id": "ez-51003",
                  "profile": "ez",
                  "diskSchemaVersions": [51003, 51004]
                }
                """);
            File.WriteAllText(Path.Combine(libDir, "osu.Game.dll"), string.Empty);

            var packages = RealmReaderPackageCatalog.Scan(root);
            var match = RealmReaderPackageCatalog.FindForSchema(packages, 51004);

            Assert.That(match, Is.Not.Null);
            Assert.That(match!.Id, Is.EqualTo("ez-51003"));
        }
    }
}
