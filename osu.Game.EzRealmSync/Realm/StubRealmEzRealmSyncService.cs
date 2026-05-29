// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzRealmSync.Abstractions;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// Phase 2 placeholder — replaced by real Realm-backed implementation.
    /// </summary>
    public sealed class StubRealmEzRealmSyncService : IEzRealmSyncService
    {
        private const string message = "真实 Realm 同步尚未实现（Phase 2）。请使用 --ui-test 或开启 UI 测试模式。";

        public Task<ValidationResult> ValidatePathsAsync(PathConfiguration paths, CancellationToken cancellationToken = default) => Task.FromResult(ValidationResult.Failure(message));

        public Task<ScanResult> ScanAsync(ScanRequest request, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default) => throw new NotImplementedException(message);

        public Task<ApplyResult> ApplyAsync(ApplyRequest request, IProgress<ApplyProgress>? progress = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException(message);

        public Task<IReadOnlyList<BackupEntry>> ListBackupsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<BackupEntry>>(Array.Empty<BackupEntry>());

        public Task RestoreBackupAsync(string backupId, IProgress<ApplyProgress>? progress = null, CancellationToken cancellationToken = default) => throw new NotImplementedException(message);
    }
}
