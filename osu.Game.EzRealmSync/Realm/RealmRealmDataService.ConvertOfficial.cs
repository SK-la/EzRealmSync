using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.IO;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm.Dynamic;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 「转回官方版」：把这整份 Ez 库收窄成官方库，产物**原地覆盖**所选文件（原文件先备份）。
    ///
    /// 全动态：源库按磁盘 schema 只读打开，目标库按官方 schema 新建，两边都不加载 osu.Game 模型。
    /// 因此任意版本（包括比本工具已知的更新的官方号）都能转，前提是有对应版本的官方 schema 快照。
    /// </summary>
    public sealed partial class RealmRealmDataService
    {
        public Task<RealmOfficialConversionResult> ConvertToOfficialRealmAsync(
            string realmId,
            string? officialSchemaSourcePath = null,
            string? backupDirectory = null,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.Run(() => convertToOfficialCore(realmId, officialSchemaSourcePath, backupDirectory, progress, cancellationToken), cancellationToken);

        private RealmOfficialConversionResult convertToOfficialCore(
            string realmId,
            string? officialSchemaSourcePath,
            string? backupDirectory,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (!registry.TryGet(realmId, out var file))
                throw new InvalidOperationException($"未找到 Realm 文件：{realmId}");

            string sourcePath = Path.GetFullPath(file.FilePath);
            int sourceSchema = RealmDiskSchemaReader.TryReadSchemaVersion(sourcePath) ?? file.SchemaVersion
                               ?? throw new InvalidOperationException($"无法读取所选库的 schema 版本：{sourcePath}");

            if (RealmSchemaSafety.Classify(sourceSchema) != RealmDiskSchemaKind.EzExtended)
                throw new InvalidOperationException("所选库不是 Ez 扩展库，无需「转回官方版」。");

            var (upstream, _) = RealmSchemaVersions.Decode(sourceSchema);

            if (upstream <= 0)
                throw new InvalidOperationException($"无法从版本号 {sourceSchema} 解出官方 upstream：{sourcePath}");

            OfficialSchemaSource official = resolveOfficialSource(upstream, officialSchemaSourcePath);

            string? guardError = Task.Run(() => RealmProcessGuard.ComprehensiveCheckAsync(sourcePath), cancellationToken).GetAwaiter().GetResult();
            if (guardError != null)
                throw new RealmUserOperationException(RealmUserErrorKind.FileInUse, guardError);

            progress?.Report(new ScanProgress { Progress = 0.05, Message = "正在创建自动备份…" });
            string backupPath = createBackupOf(sourcePath, backupDirectory);

            string tempRoot = EzRealmSyncDataPaths.CreateTempSubdirectory("official-convert");

            try
            {
                string tempTargetPath = Path.Combine(tempRoot, Path.GetFileName(sourcePath));

                OfficialConvertStats stats = DynamicOfficialConverter.Convert(sourcePath, official, tempTargetPath, progress, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new ScanProgress { Progress = 0.98, Message = "正在覆盖原文件…" });

                // 覆盖写在最后一步：搬运或自检失败时原文件一字未动，用户手里的库仍然可用。
                File.Move(tempTargetPath, sourcePath, overwrite: true);
                invalidateAfterMutatingRealm(realmId, sourcePath);

                progress?.Report(new ScanProgress { Progress = 1, Message = "转换完成" });

                return new RealmOfficialConversionResult
                {
                    TargetRealmFilePath = sourcePath,
                    SourceSchemaVersion = sourceSchema,
                    TargetSchemaVersion = stats.UpstreamVersion,
                    AppliedCount = stats.RowsWritten,
                    BackupPath = backupPath,
                    SchemaSourceDescription = official.Description,
                    DroppedClasses = stats.DroppedClasses,
                    DroppedColumns = stats.DroppedColumns,
                };
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
        }

        /// <summary>
        /// 收窄前的自动备份。配置了备份目录就进那里（时间戳命名，沿用全工具的备份规范）；
        /// 没配置则退化为「同目录 + 文件名加时间戳后缀」——用户明确要求"没设置备份文件夹时也要留一份原件"，
        /// 而覆盖是不可逆的，所以这条退路不能省。
        /// </summary>
        private static string createBackupOf(string sourcePath, string? backupDirectory)
        {
            if (!string.IsNullOrWhiteSpace(backupDirectory))
                return RealmFileBackup.CreateTimestampedCopy(sourcePath, backupDirectory);

            string stamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
            string beside = Path.Combine(
                Path.GetDirectoryName(sourcePath)!,
                $"{Path.GetFileNameWithoutExtension(sourcePath)}_ezbackup_{stamp}{Path.GetExtension(sourcePath)}");

            File.Copy(sourcePath, beside, overwrite: false);
            return beside;
        }

        private static OfficialSchemaSource resolveOfficialSource(int upstream, string? officialSchemaSourcePath)        {
            if (!string.IsNullOrWhiteSpace(officialSchemaSourcePath))
                return OfficialSchemaSourceResolver.FromFile(officialSchemaSourcePath, upstream);

            if (OfficialSchemaSourceResolver.TryFromSnapshots(upstream, out OfficialSchemaSource? source, out string? error))
                return source!;

            throw new RealmUserOperationException(RealmUserErrorKind.SchemaModelMismatch, error!);
        }
    }
}
