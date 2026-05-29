#if HAS_EZ_OSU_GAME
using osu.Game.EzRealmSync.Abstractions;
using osu.Game.EzRealmSync.IO;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// Phase 2.4：真实 Realm 数据浏览、备份与双库集合比对。
    /// </summary>
    public sealed partial class RealmRealmDataService : IRealmDataService, IRealmFixService, IRealmExportService
    {
        private readonly RealmFileRegistry registry = new();
        private readonly Dictionary<string, RealmSnapshot> snapshotCache = new();

        public Task<IReadOnlyList<RealmFileEntry>> DiscoverRealmFilesAsync(string? searchDirectory, CancellationToken cancellationToken = default) =>
            Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return registry.MergeDiscovered(searchDirectory);
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
            if (!registry.TryGet(sourceRealmId, out var sourceFile))
                throw new InvalidOperationException($"未找到源 Realm：{sourceRealmId}");

            if (!registry.TryGet(targetRealmId, out var targetFile))
                throw new InvalidOperationException($"未找到目标 Realm：{targetRealmId}");

            var kinds = RealmSetCompareHelper.ToEntityKinds(entityFilter);

            progress?.Report(new ScanProgress { Progress = 0, Message = "正在读取源库…" });
            cancellationToken.ThrowIfCancellationRequested();

            using var sourceAccess = RealmSchemaProbe.Open(sourceFile.FilePath, sourceFile.SchemaVersion);
            var sourceSnapshot = RealmDiffReader.Read(sourceAccess, progress, cancellationToken);

            progress?.Report(new ScanProgress { Progress = 0.5, Message = "正在读取目标库…" });
            cancellationToken.ThrowIfCancellationRequested();

            using var targetAccess = RealmSchemaProbe.Open(targetFile.FilePath, targetFile.SchemaVersion);
            var targetSnapshot = RealmDiffReader.Read(targetAccess, progress, cancellationToken);

            var diff = RealmDiffEngine.Compare(sourceSnapshot, targetSnapshot, kinds, progress, cancellationToken);
            return RealmSetCompareHelper.ApplyOperation(diff, operation);
        }
    }
}
#endif
