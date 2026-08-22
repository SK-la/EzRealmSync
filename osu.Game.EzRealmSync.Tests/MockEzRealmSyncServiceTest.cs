using NUnit.Framework;
using osu.Game.EzRealmSync.IO;
using osu.Game.EzRealmSync.Mock;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class MockEzRealmSyncServiceTest
    {
        [Test]
        public async Task CreateTimestampedBackupAsync_creates_real_copy()
        {
            string root = Path.Combine(Path.GetTempPath(), "EzRealmSyncTests", Guid.NewGuid().ToString("N"));
            string source = Path.Combine(root, "client.realm");
            string backupDir = Path.Combine(root, "backups");
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(source, "realm-data");

            var service = new MockEzRealmSyncService(new MockEzRealmSyncOptions { SimulatedDelayMilliseconds = 0 });
            string backupPath = await service.CreateTimestampedBackupAsync(source, backupDir);

            Assert.That(File.Exists(backupPath), Is.True);
            Assert.That(await File.ReadAllTextAsync(source), Is.EqualTo("realm-data"));
            Assert.That(await File.ReadAllTextAsync(backupPath), Is.EqualTo("realm-data"));

            try { Directory.Delete(root, true); }
            catch { }
        }

        [Test]
        public async Task CompareRealmSets_symmetricDifference_excludes_shared_hashes()
        {
            var service = new MockEzRealmSyncService(new MockEzRealmSyncOptions { SimulatedDelayMilliseconds = 0, DatasetSize = MockDatasetSize.Medium });
            var files = await service.DiscoverRealmFilesAsync(null);
            Assert.That(files, Has.Count.GreaterThanOrEqualTo(2));

            string sourceId = files[0].Id;
            string targetId = files[1].Id;

            var result = await service.CompareRealmSetsAsync(
                RealmSetOperation.SymmetricDifference,
                sourceId,
                targetId,
                EntityKindFilter.All);

            var sourceHashes = result.SourceOnly.Select(i => i.Hash).ToHashSet();
            var targetHashes = result.TargetOnly.Select(i => i.Hash).ToHashSet();
            Assert.That(sourceHashes.Overlaps(targetHashes), Is.False);
        }

        [Test]
        public async Task ApplyAsync_with_backup_records_backup_path()
        {
            var service = new MockEzRealmSyncService(new MockEzRealmSyncOptions { SimulatedDelayMilliseconds = 0, DatasetSize = MockDatasetSize.Medium });
            var files = await service.DiscoverRealmFilesAsync(null);
            var scan = await service.CompareRealmSetsAsync(
                RealmSetOperation.SymmetricDifference,
                files[0].Id,
                files[1].Id,
                EntityKindFilter.All);
            var first = scan.SourceOnly.First();

            var apply = await service.ApplyAsync(new ApplyRequest
            {
                Direction = SyncDirection.EzToOfficial,
                ItemIds = new[] { first.Id },
                CreateBackup = true,
                DeleteFromSource = false,
            });

            Assert.That(apply.AppliedCount, Is.EqualTo(1));
            Assert.That(apply.BackupPath, Is.Not.Null.And.Not.Empty);

            var backups = await service.ListBackupsAsync();
            Assert.That(backups.Any(b => b.Path == apply.BackupPath), Is.True);
        }

        [Test]
        public async Task ValidatePathsAsync_process_locked_returns_failure()
        {
            var service = new MockEzRealmSyncService(new MockEzRealmSyncOptions { ErrorInjection = MockErrorInjection.ProcessLocked });
            var result = await service.ValidatePathsAsync(new PathConfiguration());
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Is.Not.Empty);
        }

        [Test]
        public async Task CollectionDb_export_then_import_merges_by_name()
        {
            string root = Path.Combine(Path.GetTempPath(), "EzRealmSyncTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            var service = new MockEzRealmSyncService(new MockEzRealmSyncOptions { SimulatedDelayMilliseconds = 0 });
            var files = await service.DiscoverRealmFilesAsync(null);
            string realmId = files[0].Id;
            var catalog = await service.LoadCatalogAsync(realmId, ExportDataKind.CollectionDb);
            Assert.That(catalog.Items, Is.Not.Empty);

            var export = await service.ExportAsync(new RealmExportRequest
            {
                RealmId = realmId,
                Kind = ExportDataKind.CollectionDb,
                ItemIds = catalog.Items.Select(i => i.Id).ToList(),
                OutputDirectory = root,
                FolderName = "collections.db",
                FilesDirectory = root,
            });

            Assert.That(File.Exists(export.OutputRoot), Is.True);
            Assert.That(Path.GetFileName(export.OutputRoot), Is.EqualTo("collections.db"));

            var read = LegacyCollectionDb.ReadFile(export.OutputRoot);
            Assert.That(read, Has.Count.EqualTo(catalog.Items.Count));
            Assert.That(read[0].BeatmapMd5Hashes, Is.Not.Empty);

            var import = await service.ImportCollectionDbAsync(realmId, export.OutputRoot);
            Assert.That(import.CollectionCount, Is.EqualTo(catalog.Items.Count));
            Assert.That(import.MergedCount, Is.EqualTo(catalog.Items.Count));
            Assert.That(import.CreatedCount, Is.EqualTo(0));

            try { Directory.Delete(root, true); }
            catch { }
        }
    }
}
