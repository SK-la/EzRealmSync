#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Tests.TestInfrastructure;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class RealmConvertOfficialSampleTest
    {
        [Test]
        public async Task ConvertToOfficialRealmAsync_path_conflict_returns_categorized_error()
        {
            RealmSampleInfo sample = pick_ez_sample_with_realm_file();
            using var writable = RealmSampleFixture.CreateWritableCopy(sample);
            var dataService = new RealmRealmDataService();
            var entry = await dataService.RegisterRealmFileAsync(writable.RealmFilePath);

            string differentOutput = Path.Combine(writable.TempDirectory, "different.realm");

            var ex = Assert.ThrowsAsync<RealmUserOperationException>((AsyncTestDelegate)(async () =>
                await dataService.ConvertToOfficialRealmAsync(entry.Id, differentOutput)));
            Assert.That(ex!.Kind, Is.EqualTo(RealmUserErrorKind.PathConflict));
        }

        [Test]
        public async Task ConvertToOfficialRealmAsync_file_locked_returns_categorized_error()
        {
            RealmSampleInfo sample = pick_ez_sample_with_realm_file();
            using var writable = RealmSampleFixture.CreateWritableCopy(sample);
            var dataService = new RealmRealmDataService();
            var entry = await dataService.RegisterRealmFileAsync(writable.RealmFilePath);

            using var lockStream = new FileStream(writable.RealmFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var ex = Assert.ThrowsAsync<RealmUserOperationException>((AsyncTestDelegate)(async () =>
                await dataService.ConvertToOfficialRealmAsync(entry.Id)));
            Assert.That(ex!.Kind, Is.EqualTo(RealmUserErrorKind.FileInUse));
        }

        [Test]
        public async Task ConvertToOfficialRealmAsync_refreshes_registry_and_schema_kind()
        {
            RealmSampleInfo sample = pick_ez_sample_with_realm_file();
            using var writable = RealmSampleFixture.CreateWritableCopy(sample);
            var dataService = new RealmRealmDataService();
            var before = await dataService.RegisterRealmFileAsync(writable.RealmFilePath);

            RealmOfficialConversionResult result = await dataService.ConvertToOfficialRealmAsync(before.Id);

            Assert.That(result.BackupPath, Is.Not.Null.And.Not.Empty);
            Assert.That(File.Exists(result.BackupPath!), Is.True);

            try
            {
                var after = await dataService.RegisterRealmFileAsync(writable.RealmFilePath);
                Assert.That(after.Id, Is.EqualTo(before.Id));
                Assert.That(after.DiskSchemaKind, Is.EqualTo(RealmDiskSchemaKind.PpyClient));
            }
            finally
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(result.BackupPath) && File.Exists(result.BackupPath))
                        File.Delete(result.BackupPath);
                }
                catch
                {
                    // 默认备份目录清理失败不影响测试主断言。
                }
            }
        }

        private static RealmSampleInfo pick_ez_sample_with_realm_file()
        {
            RealmSampleInfo? sample = RealmSampleFixture
                                      .GetAllSamples()
                                      .FirstOrDefault(s => s.RealmFileExists
                                                        && string.Equals(s.DiskSchemaKind, nameof(RealmDiskSchemaKind.EzExtended), StringComparison.OrdinalIgnoreCase));
            if (sample == null)
                Assert.Ignore("未放置可用 Ez 样本（manifest expected.diskSchemaKind=EzExtended 且 realm 文件存在）。");

            return sample!;
        }
    }
}
#endif
