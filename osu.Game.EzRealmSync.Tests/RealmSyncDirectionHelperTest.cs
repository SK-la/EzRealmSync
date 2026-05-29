using NUnit.Framework;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmSyncDirectionHelperTest
    {
        [Test]
        public void TryResolveWritePlan_ez_to_ppy_on_A_B()
        {
            var ez = entry(51_006);
            var ppy = entry(51);

            Assert.That(RealmSyncDirectionHelper.TryResolveWritePlan(ez, ppy, out var direction, out var paths, out _), Is.True);
            Assert.That(direction, Is.EqualTo(SyncDirection.EzToOfficial));
            Assert.That(paths.SourceRealmFilePath, Is.EqualTo(ez.FilePath));
            Assert.That(paths.TargetRealmFilePath, Is.EqualTo(ppy.FilePath));
        }

        [Test]
        public void TryResolveWritePlan_same_ez_versions()
        {
            var older = entry(51_003);
            var newer = entry(51_006);

            Assert.That(RealmSyncDirectionHelper.TryResolveWritePlan(older, newer, out var direction, out _, out _), Is.True);
            Assert.That(direction, Is.EqualTo(SyncDirection.EzToEz));
        }

        private static RealmFileEntry entry(int schema)
        {
            const string dataDir = @"C:\osu\storage\data";
            return new RealmFileEntry
            {
                Id = "test",
                DisplayName = "client.realm",
                FilePath = Path.Combine(dataDir, "client.realm"),
                DataDirectory = dataDir,
                SchemaVersion = schema,
            };
        }
    }
}
