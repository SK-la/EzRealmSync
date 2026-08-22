using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Abstractions
{
    public interface IRealmDataService
    {
        Task<IReadOnlyList<RealmFileEntry>> DiscoverRealmFilesAsync(string? searchDirectory, CancellationToken cancellationToken = default);

        Task<RealmFileEntry> RegisterRealmFileAsync(string realmFilePath, CancellationToken cancellationToken = default);

        Task<RealmSnapshot> LoadRealmSnapshotAsync(string realmId, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default);

        Task<string> CreateTimestampedBackupAsync(string realmFilePath, string backupDirectory, CancellationToken cancellationToken = default);

        /// <summary>数据 Tab：从 Realm 删除谱面集 / 成绩 / 收藏夹（不迁移 schema）。</summary>
        Task<int> DeleteBrowseEntitiesAsync(
            string realmId,
            RealmObjectClass objectClass,
            IReadOnlyList<Guid> entityIds,
            CancellationToken cancellationToken = default);

        /// <summary>从 osu!stable <c>collection.db</c> / <c>collections.db</c> 按名称合并收藏夹到目标库。</summary>
        Task<RealmCollectionDbImportResult> ImportCollectionDbAsync(
            string realmId,
            string collectionDbPath,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default);
    }
}
