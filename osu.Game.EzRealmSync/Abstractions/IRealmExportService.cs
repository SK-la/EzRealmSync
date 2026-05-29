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
    }
}
