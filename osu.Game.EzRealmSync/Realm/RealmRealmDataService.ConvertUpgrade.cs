#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.IO;
using osu.Game.EzRealmSync.Models;
using osu.Game.Models;
using osu.Game.Scoring;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 「转回官方版」与「升级 Realm 文件」两条**写 schema** 的路径。
    /// 它们必然要用官方模型镜像 schema，因此留在 <c>HAS_EZ_OSU_GAME</c> 下；
    /// 计划中的后续步骤会分别把它们改成动态源读取、或整条功能下线。
    /// </summary>
    public sealed partial class RealmRealmDataService
    {
        public Task<RealmOfficialConversionResult> ConvertToOfficialRealmAsync(
            string realmId,
            OfficialConvertTarget convertTarget,
            string? outputRealmFilePath = null,
            string? backupDirectory = null,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.Run(() => convertToOfficialCore(realmId, convertTarget, outputRealmFilePath, backupDirectory, progress, cancellationToken), cancellationToken);

        public Task<RealmSchemaUpgradeResult> UpgradeSchemaToLatestAsync(
            string realmId,
            string? backupDirectory = null,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.Run(() => upgradeSchemaCore(realmId, backupDirectory, progress, cancellationToken), cancellationToken);

        private RealmSchemaUpgradeResult upgradeSchemaCore(
            string realmId,
            string? backupDirectory,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (!registry.TryGet(realmId, out var file))
                throw new InvalidOperationException($"未找到 Realm 文件：{realmId}");

            string realmPath = Path.GetFullPath(file.FilePath);
            string? guardError = Task.Run(() => RealmProcessGuard.ComprehensiveCheckAsync(realmPath), cancellationToken).GetAwaiter().GetResult();
            if (guardError != null)
                throw new RealmUserOperationException(RealmUserErrorKind.FileInUse, guardError);

            progress?.Report(new ScanProgress { Progress = 0.05, Message = "正在创建自动备份…" });
            string backupDir = string.IsNullOrWhiteSpace(backupDirectory)
                ? EzRealmSyncDefaults.DefaultBackupDirectory
                : backupDirectory;
            string backupPath = RealmFileBackup.CreateTimestampedCopy(realmPath, backupDir);

            cancellationToken.ThrowIfCancellationRequested();

            var result = RealmSchemaUpgrader.UpgradeInPlace(realmPath, file.SchemaVersion, progress, cancellationToken, backupPath);
            invalidateAfterMutatingRealm(realmId, realmPath);

            return new RealmSchemaUpgradeResult
            {
                RealmFilePath = result.RealmFilePath,
                SourceSchemaVersion = result.SourceSchemaVersion,
                TargetSchemaVersion = result.TargetSchemaVersion,
                BackupPath = backupPath,
                AlreadyUpToDate = result.AlreadyUpToDate,
            };
        }

        private RealmOfficialConversionResult convertToOfficialCore(
            string realmId,
            OfficialConvertTarget convertTarget,
            string? outputRealmFilePath,
            string? backupDirectory,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (!registry.TryGet(realmId, out var file))
                throw new InvalidOperationException($"未找到 Realm 文件：{realmId}");

            if (RealmSchemaSafety.Classify(file.SchemaVersion) != RealmDiskSchemaKind.EzExtended)
                throw new InvalidOperationException("所选库不是 Ez 扩展库，无需“转回官方版”。");

            string sourcePath = Path.GetFullPath(file.FilePath);
            string sourceName = Path.GetFileName(sourcePath);
            if (!string.IsNullOrWhiteSpace(outputRealmFilePath)
                && !string.Equals(Path.GetFullPath(outputRealmFilePath), sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new RealmUserOperationException(
                    RealmUserErrorKind.PathConflict,
                    "“转回官方版”仅支持原地转换：会先自动备份，再覆盖所选 Realm 文件本身。");
            }
            string? guardError = Task.Run(() => RealmProcessGuard.ComprehensiveCheckAsync(sourcePath), cancellationToken).GetAwaiter().GetResult();
            if (guardError != null)
                throw new RealmUserOperationException(RealmUserErrorKind.FileInUse, guardError);

            progress?.Report(new ScanProgress { Progress = 0.05, Message = "正在创建自动备份…" });
            string backupDir = string.IsNullOrWhiteSpace(backupDirectory)
                ? EzRealmSyncDefaults.DefaultBackupDirectory
                : backupDirectory;
            string backupPath = RealmFileBackup.CreateTimestampedCopy(sourcePath, backupDir);

            int sourceSchema = RealmDiskSchemaReader.TryReadSchemaVersion(sourcePath)
                               ?? file.SchemaVersion
                               ?? throw new InvalidOperationException($"无法读取所选库的 schema 版本：{sourcePath}");

            int targetOfficialUpstream = OfficialConvertPlanner.ResolveTargetOfficialUpstream(sourceSchema, convertTarget);

            string tempRoot = EzRealmSyncDataPaths.CreateTempSubdirectory("official-convert");
            string tempTargetPath = Path.Combine(tempRoot, sourceName);
            Directory.CreateDirectory(tempRoot);

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                progress?.Report(new ScanProgress { Progress = 0.15, Message = "正在读取 Ez 源库并构建官方 DTO…" });

                using var sourceOpener = RealmOfficialConvertSourceOpener.Open(sourcePath, sourceSchema, backupPath, progress, cancellationToken);
                int sourceFileCount = 0;
                sourceOpener.Access.Run(r => sourceFileCount = r.All<RealmFile>().Count());

                var job = OfficialConvertJobExporter.Export(sourceOpener.Access, targetOfficialUpstream, tempTargetPath);

                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new ScanProgress { Progress = 0.35, Message = $"正在镜像 Schema 写库（官方 upstream {targetOfficialUpstream}）…" });
                OfficialConvertResult writeResult = OfficialWriteProcessRunner.Run(job, cancellationToken);

                if (!writeResult.Success)
                {
                    throw new RealmUserOperationException(
                        RealmUserErrorKind.SchemaModelMismatch,
                        writeResult.ErrorMessage ?? "镜像写库 Worker 失败。");
                }

                progress?.Report(new ScanProgress { Progress = 0.88, Message = "正在校验官方镜像 schema…" });
                OfficialMirrorSchemaVerifier.Verify(tempTargetPath, targetOfficialUpstream, sourceFileCount);

                int targetSchema = writeResult.TargetSchemaVersion;

                progress?.Report(new ScanProgress { Progress = 0.9, Message = "正在覆盖原文件…" });
                File.Copy(tempTargetPath, sourcePath, overwrite: true);

                invalidateAfterMutatingRealm(realmId, sourcePath);

                progress?.Report(new ScanProgress { Progress = 1, Message = "转换完成" });

                return new RealmOfficialConversionResult
                {
                    TargetRealmFilePath = sourcePath,
                    AppliedCount = writeResult.AppliedCount,
                    BackupPath = backupPath,
                    TargetSchemaVersion = targetSchema,
                    ConvertTarget = convertTarget,
                    FilterStats = job.FilterStats,
                };
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
#endif
