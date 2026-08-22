#if HAS_EZ_OSU_GAME
using osu.Game.EzRealmSync.Abstractions;
using osu.Game.EzRealmSync.IO;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm.Readers;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// Phase 2.4：真实 Realm 数据浏览、备份与双库集合比对。
    /// </summary>
    public sealed partial class RealmRealmDataService : IRealmDataService, IRealmFixService, IRealmExportService
    {
        private readonly RealmFileRegistry registry = new RealmFileRegistry();
        private readonly Dictionary<string, RealmSnapshot> snapshotCache = new Dictionary<string, RealmSnapshot>();

        public Task<IReadOnlyList<RealmFileEntry>> DiscoverRealmFilesAsync(string? searchDirectory, CancellationToken cancellationToken = default) =>
            Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return registry.MergeDiscovered(searchDirectory, RealmSchemaProbe.TryReadSchemaVersion);
            }, cancellationToken);

        public Task<RealmFileEntry> RegisterRealmFileAsync(string realmFilePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!RealmSyncPathHelper.TryValidateRealmFileAccessible(realmFilePath, out string? error))
                throw new InvalidOperationException(error ?? "无法访问 Realm 文件。");

            return Task.FromResult(registry.Register(realmFilePath, RealmSchemaProbe.TryReadSchemaVersion));
        }

        public Task<RealmSnapshot> LoadRealmSnapshotAsync(string realmId, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default) =>
            Task.Run(() => loadCore(realmId, progress, cancellationToken), cancellationToken);

        private RealmSnapshot loadCore(string realmId, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
        {
            if (!registry.TryGet(realmId, out var file))
                throw new InvalidOperationException($"未找到 Realm 文件：{realmId}");

            if (snapshotCache.TryGetValue(realmId, out var cached))
                return cached;

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ScanProgress { Progress = 0, Message = $"正在打开 {file.DisplayName}…" });

            using var access = RealmSchemaProbe.Open(file.FilePath, file.SchemaVersion);
            var snapshot = RealmSnapshotBuilder.Build(file, access, progress, cancellationToken);
            snapshotCache[realmId] = snapshot;
            return snapshot;
        }

        public Task<string> CreateTimestampedBackupAsync(string realmFilePath, string backupDirectory, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(RealmFileBackup.CreateTimestampedCopy(realmFilePath, backupDirectory));
        }

        public Task<ScanResult> CompareRealmSetsAsync(
            RealmSetOperation operation,
            string sourceRealmId,
            string targetRealmId,
            EntityKindFilter entityFilter,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.Run(() => compareCore(operation, sourceRealmId, targetRealmId, entityFilter, progress, cancellationToken), cancellationToken);

        private ScanResult compareCore(
            RealmSetOperation operation,
            string sourceRealmId,
            string targetRealmId,
            EntityKindFilter entityFilter,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            RealmReaderRegistry.Instance.Refresh();

            if (!registry.TryGet(sourceRealmId, out var sourceFile))
                throw new InvalidOperationException($"未找到源 Realm：{sourceRealmId}");

            if (!registry.TryGet(targetRealmId, out var targetFile))
                throw new InvalidOperationException($"未找到目标 Realm：{targetRealmId}");

            var kinds = RealmSetCompareHelper.ToEntityKinds(entityFilter);

            progress?.Report(new ScanProgress { Progress = 0, Message = "正在读取源库…" });
            cancellationToken.ThrowIfCancellationRequested();

            int sourceSchema = sourceFile.SchemaVersion ?? RealmSchemaProbe.TryReadSchemaVersion(sourceFile.FilePath)
                ?? throw new InvalidOperationException($"无法读取 Realm schema 版本：{sourceFile.FilePath}");

            var sourceSnapshot = RealmDiffSnapshotProvider.Read(sourceFile.FilePath, sourceSchema, kinds, progress, cancellationToken);

            progress?.Report(new ScanProgress { Progress = 0.5, Message = "正在读取目标库…" });
            cancellationToken.ThrowIfCancellationRequested();

            int targetSchema = targetFile.SchemaVersion ?? RealmSchemaProbe.TryReadSchemaVersion(targetFile.FilePath)
                ?? throw new InvalidOperationException($"无法读取 Realm schema 版本：{targetFile.FilePath}");

            var targetSnapshot = RealmDiffSnapshotProvider.Read(targetFile.FilePath, targetSchema, kinds, progress, cancellationToken);

            var diff = RealmDiffEngine.Compare(sourceSnapshot, targetSnapshot, kinds, progress, cancellationToken);
            return RealmSetCompareHelper.ApplyOperation(diff, operation);
        }

        private void invalidateAfterMutatingRealm(string realmId, string realmFilePath)
        {
            snapshotCache.Remove(realmId);
            exportCatalogs.Clear();
            fixIssuesByRealm.Remove(realmId);
            registry.Register(realmFilePath, RealmSchemaProbe.TryReadSchemaVersion);
        }
    }
}
#endif
