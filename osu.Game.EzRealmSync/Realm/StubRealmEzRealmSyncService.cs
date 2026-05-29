using osu.Game.EzRealmSync.Abstractions;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// Phase 2 placeholder — replaced by real Realm-backed implementation.
    /// </summary>
    public sealed class StubRealmEzRealmSyncService : IEzRealmSyncService
    {
        private const string message = "未检测到 lib/osu.Game.dll，无法使用 RealmAccess 等 Ez osu.Game API。请将 Ez2Lazer 构建产物放入 lib/（见 lib/README.md），或使用 --ui-test。";

        public Task<ValidationResult> ValidatePathsAsync(PathConfiguration paths, CancellationToken cancellationToken = default) => Task.FromResult(ValidationResult.Failure(message));

        public Task<ScanResult> ScanAsync(ScanRequest request, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default) => throw new NotImplementedException(message);

        public Task<ApplyResult> ApplyAsync(ApplyRequest request, IProgress<ApplyProgress>? progress = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException(message);

        public Task<IReadOnlyList<BackupEntry>> ListBackupsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<BackupEntry>>(Array.Empty<BackupEntry>());

        public Task RestoreBackupAsync(string backupId, IProgress<ApplyProgress>? progress = null, CancellationToken cancellationToken = default) => throw new NotImplementedException(message);
    }
}
