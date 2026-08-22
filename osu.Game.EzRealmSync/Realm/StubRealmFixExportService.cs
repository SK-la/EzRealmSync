using osu.Game.EzRealmSync.Abstractions;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    public sealed class StubRealmFixExportService : IRealmFixService, IRealmExportService
    {
        private const string message = "未检测到 lib/osu.Game.dll。请使用 --ui-test，或将官方 osu.Game 放入 lib/。";

        public Task<IReadOnlyList<RealmFixIssue>> ScanIssuesAsync(
            string realmId,
            string workspacePath,
            RealmFixScanOptions options,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException(message);

        public Task<RealmFixApplyResult> ApplyFixesAsync(
            string realmId,
            string workspacePath,
            IReadOnlyList<Guid> issueIds,
            RealmFixApplyOptions options,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException(message);

        public Task<RealmOfficialConversionResult> ConvertToOfficialRealmAsync(
            string realmId,
            OfficialConvertTarget convertTarget,
            string? outputRealmFilePath = null,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException(message);

        public Task<RealmSchemaUpgradeResult> UpgradeSchemaToLatestAsync(
            string realmId,
            string? backupDirectory = null,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException(message);

        public void InvalidateCatalog(string? realmId = null)
        {
        }

        public Task<RealmExportCatalog> LoadCatalogAsync(
            string realmId,
            ExportDataKind kind,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException(message);

        public Task<RealmExportResult> ExportAsync(
            RealmExportRequest request,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException(message);

        public Task<RealmExportResult> ExportBrowseEntitiesAsync(
            string realmId,
            string filesDirectory,
            RealmObjectClass objectClass,
            IReadOnlyList<Guid> entityIds,
            string outputDirectory,
            string? folderName = null,
            bool groupScoresByPlayer = true,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException(message);
    }
}
