#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Abstractions;
using osu.Game.EzRealmSync.IO;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// Phase 2：通过 lib/osu.Game.dll 中的 RealmAccess / OfficialRealmAccess 实现真实 Diff/同步。
    /// </summary>
    public sealed class RealmEzRealmSyncService : IEzRealmSyncService
    {
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
            Task.Run(() => applyCore(request, progress, cancellationToken), cancellationToken);

        private static ApplyResult applyCore(ApplyRequest request, IProgress<ApplyProgress>? progress, CancellationToken cancellationToken)
        {
            string? validationError = RealmApplySupport.ValidateApplyRequest(request);
            if (validationError != null)
                throw new InvalidOperationException(validationError);

            string ezPath = request.Paths.EzRealmFile;
            string officialPath = request.Paths.OfficialRealmFile;

            string? backupPath = null;

            if (request.CreateBackup)
            {
                string targetPath = request.Direction switch
                {
                    SyncDirection.EzToOfficial => officialPath,
                    SyncDirection.OfficialToEz => ezPath,
                    _ => throw new ArgumentOutOfRangeException(nameof(request)),
                };

                string backupDir = string.IsNullOrWhiteSpace(request.BackupDirectory)
                    ? EzRealmSyncDefaults.DefaultBackupDirectory
                    : request.BackupDirectory;

                backupPath = RealmFileBackup.CreateTimestampedCopy(targetPath, backupDir);
            }

            using var sourceAccess = openSourceAccess(request.Direction, ezPath, officialPath);
            using var targetAccess = openTargetAccess(request.Direction, ezPath, officialPath);

            var result = RealmRowCopier.Apply(request, sourceAccess, targetAccess, progress, cancellationToken);
            return new ApplyResult { AppliedCount = result.AppliedCount, BackupPath = backupPath };
        }

        private static RealmAccess openSourceAccess(SyncDirection direction, string ezPath, string officialPath) => direction switch
        {
            SyncDirection.EzToOfficial => RealmDiffReader.OpenEzRealm(ezPath),
            SyncDirection.OfficialToEz => RealmDiffReader.OpenOfficialRealm(officialPath),
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };

        private static RealmAccess openTargetAccess(SyncDirection direction, string ezPath, string officialPath) => direction switch
        {
            SyncDirection.EzToOfficial => RealmDiffReader.OpenOfficialRealm(officialPath),
            SyncDirection.OfficialToEz => RealmDiffReader.OpenEzRealm(ezPath),
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };

        public Task<IReadOnlyList<BackupEntry>> ListBackupsAsync(string? backupDirectory = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string directory = string.IsNullOrWhiteSpace(backupDirectory) ? EzRealmSyncDefaults.DefaultBackupDirectory : backupDirectory;
            return Task.FromResult(RealmBackupCatalog.List(directory));
        }

        public Task RestoreBackupAsync(
            string backupId,
            string targetRealmFilePath,
            string? backupDirectory = null,
            string? safetyBackupDirectory = null,
            IProgress<ApplyProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.Run(() => restoreCore(backupId, targetRealmFilePath, backupDirectory, safetyBackupDirectory, progress, cancellationToken), cancellationToken);

        private static void restoreCore(
            string backupId,
            string targetRealmFilePath,
            string? backupDirectory,
            string? safetyBackupDirectory,
            IProgress<ApplyProgress>? progress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ApplyProgress { Progress = 0, Message = "正在定位备份…" });

            string directory = string.IsNullOrWhiteSpace(backupDirectory) ? EzRealmSyncDefaults.DefaultBackupDirectory : backupDirectory;

            if (!RealmBackupCatalog.TryFind(directory, backupId, out var entry))
                throw new InvalidOperationException($"找不到备份：{backupId}");

            progress?.Report(new ApplyProgress { Progress = 0.3, Message = "正在还原…" });
            cancellationToken.ThrowIfCancellationRequested();

            RealmFileBackup.RestoreOverTarget(entry.Path, targetRealmFilePath, safetyBackupDirectory);

            progress?.Report(new ApplyProgress { Progress = 1, Message = "还原完成" });
        }
    }
}
#endif
