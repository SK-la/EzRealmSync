#if HAS_EZ_OSU_GAME
using osu.Game.EzRealmSync.Abstractions;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// Phase 2：通过 lib/osu.Game.dll 中的 RealmAccess / OfficialRealmAccess 实现真实 Diff/同步。
    /// </summary>
    public sealed class RealmEzRealmSyncService : IEzRealmSyncService
    {
        private const string not_implemented = "RealmEzRealmSyncService 尚未实现具体 Diff/写入逻辑（P2.2–P2.4）。";

        public Task<ValidationResult> ValidatePathsAsync(PathConfiguration paths, CancellationToken cancellationToken = default) =>
            Task.FromResult(ValidationResult.Failure(not_implemented));

        public Task<ScanResult> ScanAsync(ScanRequest request, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException(not_implemented);

        public Task<ApplyResult> ApplyAsync(ApplyRequest request, IProgress<ApplyProgress>? progress = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException(not_implemented);

        public Task<IReadOnlyList<BackupEntry>> ListBackupsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BackupEntry>>(Array.Empty<BackupEntry>());

        public Task RestoreBackupAsync(string backupId, IProgress<ApplyProgress>? progress = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException(not_implemented);
    }
}

#endif
