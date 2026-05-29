using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Abstractions
{
    public interface IEzRealmSyncService
    {
        Task<ValidationResult> ValidatePathsAsync(PathConfiguration paths, CancellationToken cancellationToken = default);

        Task<ScanResult> ScanAsync(ScanRequest request, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default);

        Task<ApplyResult> ApplyAsync(ApplyRequest request, IProgress<ApplyProgress>? progress = null, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<BackupEntry>> ListBackupsAsync(CancellationToken cancellationToken = default);

        Task RestoreBackupAsync(string backupId, IProgress<ApplyProgress>? progress = null, CancellationToken cancellationToken = default);
    }
}
