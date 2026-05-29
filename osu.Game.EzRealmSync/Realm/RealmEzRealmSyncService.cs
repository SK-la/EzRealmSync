#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Abstractions;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// Phase 2：通过 lib/osu.Game.dll 中的 RealmAccess / OfficialRealmAccess 实现真实 Diff/同步。
    /// </summary>
    public sealed class RealmEzRealmSyncService : IEzRealmSyncService
    {
        private readonly List<BackupEntry> backups = new();

        public Task<ValidationResult> ValidatePathsAsync(PathConfiguration paths, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var errors = new List<string>();
            var warnings = new List<string>();

            if (!RealmSyncPathHelper.TryValidateRealmFileAccessible(paths.EzRealmFile, out string? ezError) && ezError != null)
                errors.Add($"Ez：{ezError}");

            if (!RealmSyncPathHelper.TryValidateRealmFileAccessible(paths.OfficialRealmFile, out string? officialError) && officialError != null)
                errors.Add($"官方：{officialError}");

            if (errors.Count == 0 && !RealmSyncPathHelper.SharedFilesDirectoriesMatch(paths.EzDataPath, paths.OfficialDataPath))
                warnings.Add("两个数据目录的 files/ 路径不一致；同步后官方客户端可能仍缺少实体文件。");

            if (errors.Count > 0)
                return Task.FromResult(ValidationResult.Failure(errors.ToArray()));

            return Task.FromResult(ValidationResult.Success(warnings.ToArray()));
        }

        public Task<ScanResult> ScanAsync(ScanRequest request, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default) =>
            Task.Run(() => scanCore(request, progress, cancellationToken), cancellationToken);

        private static ScanResult scanCore(ScanRequest request, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
        {
            var paths = request.Paths;

            string sourcePath = request.Direction switch
            {
                SyncDirection.EzToOfficial => paths.EzRealmFile,
                SyncDirection.OfficialToEz => paths.OfficialRealmFile,
                _ => throw new ArgumentOutOfRangeException(nameof(request)),
            };

            string targetPath = request.Direction switch
            {
                SyncDirection.EzToOfficial => paths.OfficialRealmFile,
                SyncDirection.OfficialToEz => paths.EzRealmFile,
                _ => throw new ArgumentOutOfRangeException(nameof(request)),
            };

            progress?.Report(new ScanProgress { Progress = 0, Message = "正在打开源库…" });
            cancellationToken.ThrowIfCancellationRequested();

            using var sourceAccess = openRealm(sourcePath, request.Direction, source: true);
            using var targetAccess = openRealm(targetPath, request.Direction, source: false);

            var sourceSnapshot = RealmDiffReader.Read(sourceAccess, progress, cancellationToken);
            progress?.Report(new ScanProgress { Progress = 0.5, Message = "正在读取目标库…" });
            var targetSnapshot = RealmDiffReader.Read(targetAccess, progress, cancellationToken);

            return RealmDiffEngine.Compare(sourceSnapshot, targetSnapshot, request.EntityKinds, progress, cancellationToken);
        }

        private static RealmAccess openRealm(string realmFilePath, SyncDirection direction, bool source)
        {
            bool ez = direction switch
            {
                SyncDirection.EzToOfficial => source,
                SyncDirection.OfficialToEz => !source,
                _ => throw new ArgumentOutOfRangeException(nameof(direction)),
            };

            return ez ? RealmDiffReader.OpenEzRealm(realmFilePath) : RealmDiffReader.OpenOfficialRealm(realmFilePath);
        }

        public Task<ApplyResult> ApplyAsync(ApplyRequest request, IProgress<ApplyProgress>? progress = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException("RealmRowCopier / 写入尚未实现（P2.3–P2.4）。");

        public Task<IReadOnlyList<BackupEntry>> ListBackupsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<BackupEntry>>(backups.ToList());
        }

        public Task RestoreBackupAsync(string backupId, IProgress<ApplyProgress>? progress = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException("备份还原尚未实现（P2.4）。");
    }
}
#endif
