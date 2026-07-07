#if HAS_EZ_OSU_GAME
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.IO;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm.Readers;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 工具侧 schema 升级：备份后复制到目标 schema 新库并原子替换，不调用游戏 <see cref="RealmAccess"/> 的 migration / 降级重建路径。
    /// </summary>
    public static class RealmSchemaUpgrader
    {
        public static RealmSchemaUpgradeResult UpgradeInPlace(
            string realmFilePath,
            int? knownDiskSchemaVersion = null,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default,
            string? backupPathForRollback = null)
        {
            int? sourceSchema = knownDiskSchemaVersion ?? RealmDiskSchemaReader.TryReadSchemaVersion(realmFilePath);
            if (sourceSchema == null)
                throw new InvalidOperationException($"无法读取 Realm schema 版本：{realmFilePath}");

            RealmDiskSchemaKind kind = RealmSchemaSafety.Classify(sourceSchema.Value);
            if (kind == RealmDiskSchemaKind.Unknown)
                throw new InvalidOperationException($"无法识别的 Realm schema 版本 {sourceSchema}：{realmFilePath}");

            int latestSupportedSchema = kind == RealmDiskSchemaKind.PpyClient
                ? RealmAccess.UpstreamSchemaVersion
                : RealmAccess.EzFileSchemaVersion;

            if (sourceSchema.Value > latestSupportedSchema)
            {
                throw new InvalidOperationException(
                    $"Realm schema {sourceSchema} 高于本工具支持的 {latestSupportedSchema}，请更新 EzRealmSync（ez2lazer.Game NuGet）后再试。");
            }

            if (sourceSchema.Value == latestSupportedSchema && canOpenWithoutMigration(realmFilePath, kind, sourceSchema.Value))
            {
                progress?.Report(new ScanProgress { Progress = 1, Message = "已是最新 schema" });
                return new RealmSchemaUpgradeResult
                {
                    RealmFilePath = realmFilePath,
                    SourceSchemaVersion = sourceSchema.Value,
                    TargetSchemaVersion = sourceSchema.Value,
                    AlreadyUpToDate = true,
                };
            }

            string fullPath = Path.GetFullPath(realmFilePath);
            string storageRoot = RealmWorkspacePaths.ResolveStorageRoot(fullPath);
            string filename = Path.GetFileName(fullPath);
            string tempRoot = Path.Combine(Path.GetTempPath(), "EzRealmSync", "schema-upgrade", Guid.NewGuid().ToString("N"));
            string tempRealmPath = Path.Combine(tempRoot, filename);

            Directory.CreateDirectory(tempRoot);

            try
            {
                progress?.Report(new ScanProgress { Progress = 0.1, Message = "正在读取源库…" });
                cancellationToken.ThrowIfCancellationRequested();

                RealmMigrationCounts beforeCounts;
                using (var sourceAccess = openPinned(fullPath, kind, sourceSchema.Value))
                    beforeCounts = RealmMigrationCounts.Capture(sourceAccess);

                progress?.Report(new ScanProgress { Progress = 0.15, Message = "正在创建目标 schema 库…" });
                RealmDirectOpener.CreateEmptyAtDiskSchema(tempRealmPath, latestSupportedSchema);

                progress?.Report(new ScanProgress { Progress = 0.2, Message = "正在复制数据到新 schema…" });
                using (var sourceAccess = openPinned(fullPath, kind, sourceSchema.Value))
                using (var targetAccess = openPinned(tempRealmPath, kind, latestSupportedSchema))
                {
                    RealmSchemaMigrationCopier.CopyAll(sourceAccess, targetAccess, kind, progress, cancellationToken);
                }

                RealmMigrationCounts afterCounts;
                using (var targetAccess = openPinned(tempRealmPath, kind, latestSupportedSchema))
                    afterCounts = RealmMigrationCounts.Capture(targetAccess);

                if (afterCounts.IsCatastrophicLossComparedTo(beforeCounts))
                {
                    throw new RealmUserOperationException(
                        RealmUserErrorKind.MigrationRequired,
                        $"迁移后数据量异常（迁移前 {beforeCounts}，迁移后 {afterCounts}）。已中止替换，请从备份恢复。");
                }

                progress?.Report(new ScanProgress { Progress = 0.95, Message = "正在替换原文件…" });
                cancellationToken.ThrowIfCancellationRequested();
                File.Copy(tempRealmPath, fullPath, overwrite: true);

                int? upgradedSchema = RealmDiskSchemaReader.TryReadSchemaVersion(fullPath) ?? latestSupportedSchema;
                progress?.Report(new ScanProgress { Progress = 1, Message = "迁移完成" });

                return new RealmSchemaUpgradeResult
                {
                    RealmFilePath = fullPath,
                    SourceSchemaVersion = sourceSchema.Value,
                    TargetSchemaVersion = upgradedSchema.Value,
                    AlreadyUpToDate = false,
                };
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(backupPathForRollback) && File.Exists(backupPathForRollback))
                {
                    try
                    {
                        RealmFileBackup.RestoreOverTarget(backupPathForRollback, fullPath);
                    }
                    catch
                    {
                        // 保留原始异常；回滚失败时用户仍可使用 backupPathForRollback 手动恢复。
                    }
                }

                throw;
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
        }

        private static bool canOpenWithoutMigration(string realmFilePath, RealmDiskSchemaKind kind, int diskSchemaVersion)
        {
            try
            {
                using var access = openPinned(realmFilePath, kind, diskSchemaVersion);
                access.Run(_ => { });
                return true;
            }
            catch (Exception ex) when (RealmOpenErrorClassifier.IsMigrationRequired(ex))
            {
                return false;
            }
        }

        private static RealmAccess openPinned(string realmFilePath, RealmDiskSchemaKind kind, int diskSchemaVersion)
        {
            string fullPath = Path.GetFullPath(realmFilePath);
            string storageRoot = RealmWorkspacePaths.ResolveStorageRoot(fullPath);
            string filename = Path.GetFileName(fullPath);
            var storage = new NativeStorage(storageRoot);

            return kind == RealmDiskSchemaKind.EzExtended
                ? RealmAccess.OpenWithoutMigration(storage, filename, diskSchemaVersion)
                : OfficialRealmAccess.OpenWithoutMigration(storage, filename, diskSchemaVersion);
        }
    }
}
#endif
