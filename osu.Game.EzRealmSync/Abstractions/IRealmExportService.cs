using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Abstractions
{
    public interface IRealmExportService
    {
        Task<RealmExportCatalog> LoadCatalogAsync(
            string realmId,
            ExportDataKind kind,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default);

        Task<RealmExportResult> ExportAsync(
            RealmExportRequest request,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>数据 Tab：导出所选谱面集 / 收藏夹对应的 files/ 实体。</summary>
        Task<RealmExportResult> ExportBrowseEntitiesAsync(
            string realmId,
            string filesDirectory,
            RealmObjectClass objectClass,
            IReadOnlyList<Guid> entityIds,
            string outputDirectory,
            string? folderName = null,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default);
    }
}
