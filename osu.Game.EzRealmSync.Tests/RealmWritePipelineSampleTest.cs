#if HAS_EZ_OSU_GAME
using System.Text.RegularExpressions;
using NUnit.Framework;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Tests.TestInfrastructure;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmWritePipelineSampleTest
    {
        [Test]
        public async Task CreateTimestampedBackupAsync_uses_second_precision_naming()
        {
            RealmSampleInfo sample = pick_any_sample_with_realm_file();
            using var writable = RealmSampleFixture.CreateWritableCopy(sample);
            string backupDirectory = Path.Combine(writable.TempDirectory, "backups");

            var dataService = new RealmRealmDataService();
            string backupPath = await dataService.CreateTimestampedBackupAsync(writable.RealmFilePath, backupDirectory);

            Assert.That(File.Exists(backupPath), Is.True);
            Assert.That(
                Path.GetFileName(backupPath),
                Does.Match(new Regex(@"^.+_\d{8}_\d{6}\.realm$", RegexOptions.CultureInvariant)));
        }

        private static RealmSampleInfo pick_any_sample_with_realm_file()
        {
            RealmSampleInfo? sample = RealmSampleFixture
                                      .GetAllSamples()
                                      .FirstOrDefault(s => s.RealmFileExists);
            if (sample == null)
                Assert.Ignore("未放置任何 .realm 样本文件，跳过样本写路径测试。");

            return sample!;
        }
    }
}
#endif
