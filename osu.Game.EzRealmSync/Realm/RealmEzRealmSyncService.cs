using osu.Game.EzRealmSync.Abstractions;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.IO;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 同步走 DynamicRealm 官方基线；不使用 osu.Game.dll 打开用户库。
    /// </summary>
    public sealed class RealmEzRealmSyncService : IEzRealmSyncService
    {
        private readonly RealmFileRegistry registry;

        public RealmEzRealmSyncService(RealmFileRegistry registry)
        {
            this.registry = registry;
        }

        public Task<ValidationResult> ValidatePathsAsync(PathConfiguration paths, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var errors = new List<string>();
            var warnings = new List<string>();

            if (!string.IsNullOrWhiteSpace(paths.SourceRealmFilePath)
                && !string.IsNullOrWhiteSpace(paths.TargetRealmFilePath))
            {
                if (!RealmSyncPathHelper.TryValidateRealmFileAccessible(paths.SourceRealmFilePath, out string? sourceError) && sourceError != null)
                    errors.Add($"源（A）：{sourceError}");

                if (!RealmSyncPathHelper.TryValidateRealmFileAccessible(paths.TargetRealmFilePath, out string? targetError) && targetError != null)
                    errors.Add($"目标（B）：{targetError}");
            }
            else
            {
                if (!RealmSyncPathHelper.TryValidateRealmFileAccessible(paths.EzRealmFile, out string? ezError) && ezError != null)
                    errors.Add($"源库：{ezError}");

                if (!RealmSyncPathHelper.TryValidateRealmFileAccessible(paths.OfficialRealmFile, out string? officialError) && officialError != null)
                    errors.Add($"目标库：{officialError}");

                if (errors.Count == 0 && !RealmSyncPathHelper.SharedFilesDirectoriesMatch(paths.EzDataPath, paths.OfficialDataPath))
                    warnings.Add("两个工作区的 files/ 路径不一致；同步后目标端可能仍缺少实体文件。");
            }

            string? processBlock = RealmProcessGuard.TryGetBlockingProcessMessage();
            if (processBlock != null)
                errors.Add(processBlock);

            if (errors.Count > 0)
                return Task.FromResult(ValidationResult.Failure(errors.ToArray()));

            return Task.FromResult(ValidationResult.Success(warnings.ToArray()));
        }

        public Task<ScanResult> CompareRealmSetsAsync(
            RealmSetOperation operation,
            string sourceRealmId,
            string targetRealmId,
            EntityKindFilter entityFilter,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.Run(() => compareCore(operation, sourceRealmId, targetRealmId, entityFilter, progress, cancellationToken), cancellationToken);

        private ScanResult compareCore(
            RealmSetOperation operation,
            string sourceRealmId,
            string targetRealmId,
            EntityKindFilter entityFilter,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (!registry.TryGet(sourceRealmId, out var sourceFile))
                throw new InvalidOperationException($"未找到源 Realm：{sourceRealmId}");

            if (!registry.TryGet(targetRealmId, out var targetFile))
                throw new InvalidOperationException($"未找到目标 Realm：{targetRealmId}");

            var kinds = RealmSetCompareHelper.ToEntityKinds(entityFilter);

            progress?.Report(new ScanProgress { Progress = 0, Message = "正在读取源库…" });
            cancellationToken.ThrowIfCancellationRequested();

            // 版本号只用于识别/展示：读全部表列由动态打开自行判定，不再按版本选路。
            var sourceSnapshot = RealmAccessGateway.ReadDiffSnapshot(sourceFile.FilePath, kinds, progress, cancellationToken);

            progress?.Report(new ScanProgress { Progress = 0.5, Message = "正在读取目标库…" });
            cancellationToken.ThrowIfCancellationRequested();

            var targetSnapshot = RealmAccessGateway.ReadDiffSnapshot(targetFile.FilePath, kinds, progress, cancellationToken);

            var diff = RealmDiffEngine.Compare(sourceSnapshot, targetSnapshot, kinds, progress, cancellationToken);
            return RealmSetCompareHelper.ApplyOperation(diff, operation);
        }

        public Task<ApplyResult> ApplyAsync(ApplyRequest request, IProgress<ApplyProgress>? progress = null, CancellationToken cancellationToken = default) =>
            Task.Run(() => applyCore(request, progress, cancellationToken), cancellationToken);

        private static ApplyResult applyCore(ApplyRequest request, IProgress<ApplyProgress>? progress, CancellationToken cancellationToken)
        {
            string? validationError = RealmApplySupport.ValidateApplyRequest(request);
            if (validationError != null)
                throw new InvalidOperationException(validationError);

            var plan = resolvePlan(request.WritePlan, request.Direction, request.Paths);

            string mutationPath = request.DeleteFromSource
                ? plan.SourceRealmFilePath
                : plan.TargetRealmFilePath;

            string? guardError = Task.Run(() => RealmProcessGuard.ComprehensiveCheckAsync(mutationPath), cancellationToken).GetAwaiter().GetResult();
            if (guardError != null)
                throw new RealmUserOperationException(RealmUserErrorKind.FileInUse, guardError);

            string? backupPath = null;

            if (request.CreateBackup)
            {
                string backupDir = string.IsNullOrWhiteSpace(request.BackupDirectory)
                    ? EzRealmSyncDefaults.DefaultBackupDirectory
                    : request.BackupDirectory;

                backupPath = RealmFileBackup.CreateTimestampedCopy(mutationPath, backupDir);
            }

            ApplyResult result;

            if (request.DeleteFromSource)
            {
                result = DynamicBaselineWriter.SoftDelete(request, mutationPath, progress, cancellationToken);
            }
            else
            {
                var bundle = DynamicBaselineReader.ExportByIds(plan.SourceRealmFilePath, request.ItemIds);
                DynamicBaselineFileCopier.CopyMissing(plan.SourceRealmFilePath, plan.TargetRealmFilePath, bundle);
                result = DynamicBaselineWriter.Apply(request, bundle, plan.TargetRealmFilePath, progress, cancellationToken);
            }

            return new ApplyResult
            {
                AppliedCount = result.AppliedCount,
                SkippedCount = result.SkippedCount,
                SkipReasons = result.SkipReasons,
                BackupPath = backupPath,
            };
        }

        private static RealmWritePlan resolvePlan(RealmWritePlan? explicitPlan, SyncDirection direction, PathConfiguration paths)
        {
            if (explicitPlan != null)
                return explicitPlan;

            if (!string.IsNullOrWhiteSpace(paths.SourceRealmFilePath)
                && !string.IsNullOrWhiteSpace(paths.TargetRealmFilePath))
            {
                return direction switch
                {
                    SyncDirection.EzToOfficial => new RealmWritePlan
                    {
                        SourceRealmFilePath = paths.SourceRealmFilePath!,
                        TargetRealmFilePath = paths.TargetRealmFilePath!,
                        SourceKind = RealmDiskSchemaKind.EzExtended,
                        TargetKind = RealmDiskSchemaKind.PpyClient,
                        LegacyDirection = direction,
                    },
                    SyncDirection.OfficialToEz => new RealmWritePlan
                    {
                        SourceRealmFilePath = paths.SourceRealmFilePath!,
                        TargetRealmFilePath = paths.TargetRealmFilePath!,
                        SourceKind = RealmDiskSchemaKind.PpyClient,
                        TargetKind = RealmDiskSchemaKind.EzExtended,
                        LegacyDirection = direction,
                    },
                    SyncDirection.EzToEz => new RealmWritePlan
                    {
                        SourceRealmFilePath = paths.SourceRealmFilePath!,
                        TargetRealmFilePath = paths.TargetRealmFilePath!,
                        SourceKind = RealmDiskSchemaKind.EzExtended,
                        TargetKind = RealmDiskSchemaKind.EzExtended,
                        LegacyDirection = direction,
                    },
                    SyncDirection.PpyToPpy => new RealmWritePlan
                    {
                        SourceRealmFilePath = paths.SourceRealmFilePath!,
                        TargetRealmFilePath = paths.TargetRealmFilePath!,
                        SourceKind = RealmDiskSchemaKind.PpyClient,
                        TargetKind = RealmDiskSchemaKind.PpyClient,
                        LegacyDirection = direction,
                    },
                    _ => throw new ArgumentOutOfRangeException(nameof(direction)),
                };
            }

            string sourcePath = direction switch
            {
                SyncDirection.EzToOfficial or SyncDirection.EzToEz => paths.EzRealmFile,
                SyncDirection.OfficialToEz or SyncDirection.PpyToPpy => paths.OfficialRealmFile,
                _ => throw new ArgumentOutOfRangeException(nameof(direction)),
            };

            string targetPath = direction switch
            {
                SyncDirection.EzToOfficial or SyncDirection.PpyToPpy => paths.OfficialRealmFile,
                SyncDirection.OfficialToEz or SyncDirection.EzToEz => paths.EzRealmFile,
                _ => throw new ArgumentOutOfRangeException(nameof(direction)),
            };

            var (sourceKind, targetKind) = direction switch
            {
                SyncDirection.EzToOfficial => (RealmDiskSchemaKind.EzExtended, RealmDiskSchemaKind.PpyClient),
                SyncDirection.OfficialToEz => (RealmDiskSchemaKind.PpyClient, RealmDiskSchemaKind.EzExtended),
                SyncDirection.EzToEz => (RealmDiskSchemaKind.EzExtended, RealmDiskSchemaKind.EzExtended),
                SyncDirection.PpyToPpy => (RealmDiskSchemaKind.PpyClient, RealmDiskSchemaKind.PpyClient),
                _ => throw new ArgumentOutOfRangeException(nameof(direction)),
            };

            return new RealmWritePlan
            {
                SourceRealmFilePath = sourcePath,
                TargetRealmFilePath = targetPath,
                SourceKind = sourceKind,
                TargetKind = targetKind,
                LegacyDirection = direction,
            };
        }

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
