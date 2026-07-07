#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;
using Realms;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmDiskSchemaReaderIntegrationTest
    {
        [Test]
        public void TryReadSchemaVersion_reads_version_from_created_realm()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"probe_{Guid.NewGuid():N}.realm");

            try
            {
                var writeConfig = new RealmConfiguration(path) { SchemaVersion = 51_006 };
                using (RealmInstance.GetInstance(writeConfig))
                {
                }

                Assert.That(RealmSchemaProbe.TryReadSchemaVersion(path), Is.EqualTo(51_006));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);

                string lockPath = path + ".lock";
                if (File.Exists(lockPath))
                    File.Delete(lockPath);
            }
        }

        [Test]
        public void TryReadSchemaVersion_reads_official_upstream_schema()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"probe_official_{Guid.NewGuid():N}.realm");

            try
            {
                var writeConfig = new RealmConfiguration(path) { SchemaVersion = 51 };
                using (RealmInstance.GetInstance(writeConfig))
                {
                }

                Assert.That(RealmSchemaProbe.TryReadSchemaVersion(path), Is.EqualTo(51));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);

                string lockPath = path + ".lock";
                if (File.Exists(lockPath))
                    File.Delete(lockPath);
            }
        }

        [Test]
        public void TryReadSchemaVersion_plain_client_realm_at_51_does_not_report_ez_suffix()
        {
            string dir = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"probe_client_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "client.realm");

            try
            {
                var writeConfig = new RealmConfiguration(path) { SchemaVersion = 51 };
                using (RealmInstance.GetInstance(writeConfig))
                {
                }

                int? schema = RealmSchemaProbe.TryReadSchemaVersion(path);
                Assert.That(schema, Is.EqualTo(51));
                Assert.That(RealmSchemaSafety.Classify(schema), Is.EqualTo(RealmDiskSchemaKind.PpyClient));
            }
            finally
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }

        [Test]
        public void TryReadSchemaVersion_infers_from_versioned_filename_when_open_fails()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "client_51003.realm");
            File.WriteAllBytes(path, Array.Empty<byte>());

            try
            {
                Assert.That(RealmSchemaProbe.TryReadSchemaVersion(path), Is.EqualTo(51_003));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}
#endif
