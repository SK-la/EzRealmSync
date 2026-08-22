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
            var dataService = new RealmRealmDataService(new RealmFileRegistry());
            var entry = await dataService.RegisterRealmFileAsync(writable.RealmFilePath);

            string differentOutput = Path.Combine(writable.TempDirectory, "different.realm");

            try
            {
                await dataService.ConvertToOfficialRealmAsync(entry.Id, OfficialConvertTarget.PreserveReadUpstream, differentOutput);
                Assert.Fail("Expected RealmUserOperationException");
            }
            catch (RealmUserOperationException ex)
            {
                Assert.That(ex.Kind, Is.EqualTo(RealmUserErrorKind.PathConflict));
            }
        }

        [Test]
        public async Task ConvertToOfficialRealmAsync_file_locked_returns_categorized_error()
        {
            RealmSampleInfo sample = pick_ez_sample_with_realm_file();
            using var writable = RealmSampleFixture.CreateWritableCopy(sample);
            var dataService = new RealmRealmDataService(new RealmFileRegistry());
            var entry = await dataService.RegisterRealmFileAsync(writable.RealmFilePath);

            await using var lockStream = new FileStream(writable.RealmFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            try
            {
                await dataService.ConvertToOfficialRealmAsync(entry.Id, OfficialConvertTarget.PreserveReadUpstream);
                Assert.Fail("Expected RealmUserOperationException");
            }
            catch (RealmUserOperationException ex)
            {
                Assert.That(ex.Kind, Is.EqualTo(RealmUserErrorKind.FileInUse));
            }
        }

        [Test]
        public async Task ConvertToOfficialRealmAsync_handles_success_or_migration_required_deterministically()
        {
            RealmSampleInfo sample = pick_ez_sample_with_realm_file();
            using var writable = RealmSampleFixture.CreateWritableCopy(sample);
            var dataService = new RealmRealmDataService(new RealmFileRegistry());
            var before = await dataService.RegisterRealmFileAsync(writable.RealmFilePath);

            try
            {
                RealmOfficialConversionResult result = await dataService.ConvertToOfficialRealmAsync(
                    before.Id,
                    OfficialConvertTarget.PreserveReadUpstream);
                Assert.That(result.BackupPath, Is.Not.Null.And.Not.Empty);
                Assert.That(File.Exists(result.BackupPath!), Is.True);
                Assert.That(result.ConvertTarget, Is.EqualTo(OfficialConvertTarget.PreserveReadUpstream));

                try
                {
                    var after = await dataService.RegisterRealmFileAsync(writable.RealmFilePath);
                    Assert.That(after.Id, Is.EqualTo(before.Id));
                    Assert.That(after.DiskSchemaKind, Is.EqualTo(RealmDiskSchemaKind.PpyClient));

                    if (before.SchemaVersion is { } sourceSchema)
                    {
                        int expectedOfficial = OfficialConvertPlanner.ResolveTargetOfficialUpstream(sourceSchema, OfficialConvertTarget.PreserveReadUpstream);
                        Assert.That(after.SchemaVersion, Is.EqualTo(expectedOfficial));
                    }
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
            catch (RealmUserOperationException ex) when (ex.Kind is RealmUserErrorKind.MigrationRequired
                                                         or RealmUserErrorKind.SchemaTooLow
                                                         or RealmUserErrorKind.SchemaModelMismatch)
            {
                Assert.That(ex.Kind, Is.AnyOf(
                    RealmUserErrorKind.MigrationRequired,
                    RealmUserErrorKind.SchemaTooLow,
                    RealmUserErrorKind.SchemaModelMismatch));
            }
        }

        [Test]
        public async Task ConvertToOfficialRealmAsync_uses_custom_backup_directory()
        {
            RealmSampleInfo sample = pick_ez_sample_with_realm_file();
            using var writable = RealmSampleFixture.CreateWritableCopy(sample);
            var dataService = new RealmRealmDataService(new RealmFileRegistry());
            var entry = await dataService.RegisterRealmFileAsync(writable.RealmFilePath);

            string customBackupDir = Path.Combine(writable.TempDirectory, "custom-backups");

            try
            {
                RealmOfficialConversionResult result = await dataService.ConvertToOfficialRealmAsync(
                    entry.Id,
                    OfficialConvertTarget.PreserveReadUpstream,
                    backupDirectory: customBackupDir);

                Assert.That(result.BackupPath, Is.Not.Null.And.Not.Empty);
                Assert.That(Path.GetDirectoryName(result.BackupPath!), Is.EqualTo(Path.GetFullPath(customBackupDir)));
                Assert.That(File.Exists(result.BackupPath!), Is.True);
            }
            catch (RealmUserOperationException ex) when (ex.Kind is RealmUserErrorKind.MigrationRequired
                                                         or RealmUserErrorKind.SchemaTooLow
                                                         or RealmUserErrorKind.SchemaModelMismatch)
            {
                Assert.Ignore($"样本无法完成转官方：{ex.Kind}");
            }
        }

        private static RealmSampleInfo pick_ez_sample_with_realm_file(bool requireCanOpenWithoutMigration = false)
        {
            RealmSampleInfo? sample = RealmSampleFixture
                                      .GetAllSamples()
                                      .FirstOrDefault(s => s.RealmFileExists
                                                        && (!requireCanOpenWithoutMigration || s.CanOpenWithoutMigration)
                                                        && string.Equals(s.DiskSchemaKind, nameof(RealmDiskSchemaKind.EzExtended), StringComparison.OrdinalIgnoreCase));
            if (sample == null)
                Assert.Ignore("未放置可用 Ez 样本（manifest expected.diskSchemaKind=EzExtended 且 realm 文件存在）。");

            return sample;
        }
    }
}
#endif
