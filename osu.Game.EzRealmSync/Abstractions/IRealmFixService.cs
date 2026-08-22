using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Abstractions
{
    public interface IRealmFixService
    {
        Task<IReadOnlyList<RealmFixIssue>> ScanIssuesAsync(
            string realmId,
            string workspacePath,
            RealmFixScanOptions options,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default);

        Task<RealmFixApplyResult> ApplyFixesAsync(
            string realmId,
            string workspacePath,
            IReadOnlyList<Guid> issueIds,
            RealmFixApplyOptions options,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default);

        Task<RealmOfficialConversionResult> ConvertToOfficialRealmAsync(
            string realmId,
            OfficialConvertTarget convertTarget,
            string? outputRealmFilePath = null,
            string? backupDirectory = null,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default);

        Task<RealmSchemaUpgradeResult> UpgradeSchemaToLatestAsync(
            string realmId,
            string? backupDirectory = null,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default);
    }
}
