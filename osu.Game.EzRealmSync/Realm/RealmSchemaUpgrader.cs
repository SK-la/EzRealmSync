#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.IO;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm.Readers;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 工具侧 schema 升级：同类型库全量 detach 复制到目标 schema 新库，不调用游戏 migration / 降级重建。
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

            if (sourceSchema.Value == latestSupportedSchema && canOpenWithoutMigration(realmFilePath, sourceSchema.Value))
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
            string filename = Path.GetFileName(fullPath);
            string tempRoot = Path.Combine(Path.GetTempPath(), "EzRealmSync", "schema-upgrade", Guid.NewGuid().ToString("N"));
            string tempRealmPath = Path.Combine(tempRoot, filename);

            Directory.CreateDirectory(tempRoot);

            try
            {
                progress?.Report(new ScanProgress { Progress = 0.05, Message = "正在统计源库行数…" });
                cancellationToken.ThrowIfCancellationRequested();

                var dynamicBefore = RealmDynamicObjectCounter.Capture(fullPath);

                RealmMigrationCounts beforeCounts;
                using (var sourceAccess = RealmSchemaProbe.Open(fullPath, sourceSchema.Value))
                    beforeCounts = RealmMigrationCounts.Capture(sourceAccess);

                if (RealmDynamicObjectCounter.TypedReadLooksIncomplete(dynamicBefore, beforeCounts))
                {
                    throw new RealmUserOperationException(
                        RealmUserErrorKind.LegacyReaderUnavailable,
                        $"无法完整读取源库（磁盘 {dynamicBefore}，typed {beforeCounts}）。请确认已关闭游戏、安装匹配 legacy reader，或先从完整备份恢复后再升级。");
                }

                progress?.Report(new ScanProgress { Progress = 0.12, Message = "正在创建目标 schema 库…" });
                RealmDirectOpener.CreateEmptyAtDiskSchema(tempRealmPath, latestSupportedSchema);

                progress?.Report(new ScanProgress
                {
                    Progress = 0.15,
                    Message = $"正在复制 {beforeCounts}…",
                });

                using (var sourceAccess = RealmSchemaProbe.Open(fullPath, sourceSchema.Value))
                using (var targetAccess = openTarget(tempRealmPath, kind, latestSupportedSchema))
                {
                    RealmSchemaMigrationCopier.CopyAll(sourceAccess, targetAccess, progress, cancellationToken);
                }

                RealmMigrationCounts afterCounts;
                using (var targetAccess = openTarget(tempRealmPath, kind, latestSupportedSchema))
                    afterCounts = RealmMigrationCounts.Capture(targetAccess);

                if (afterCounts.IsCatastrophicLossComparedTo(beforeCounts)
                    || dynamicBefore.Files > 0 && afterCounts.RealmFiles < dynamicBefore.Files * 0.99
                    || dynamicBefore.Rulesets > 0 && afterCounts.Rulesets < dynamicBefore.Rulesets
                    || dynamicBefore.Skins > 0 && afterCounts.Skins < dynamicBefore.Skins)
                {
                    throw new RealmUserOperationException(
                        RealmUserErrorKind.MigrationRequired,
                        $"迁移后数据量异常（磁盘前 {dynamicBefore}，typed 前 {beforeCounts}，迁移后 {afterCounts}）。已中止替换，请从备份恢复。");
                }

                progress?.Report(new ScanProgress { Progress = 0.97, Message = "正在替换原文件…" });
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

        private static bool canOpenWithoutMigration(string realmFilePath, int diskSchemaVersion)
        {
            try
            {
                using var access = RealmSchemaProbe.Open(realmFilePath, diskSchemaVersion);
                access.Run(_ => { });
                return true;
            }
            catch (Exception ex) when (RealmOpenErrorClassifier.IsMigrationRequired(ex))
            {
                return false;
            }
        }

        private static RealmAccess openTarget(string realmFilePath, RealmDiskSchemaKind kind, int diskSchemaVersion) =>
            kind == RealmDiskSchemaKind.EzExtended
                ? RealmDiffReader.OpenEzRealm(realmFilePath, diskSchemaVersion)
                : RealmDiffReader.OpenOfficialRealm(realmFilePath, diskSchemaVersion);
    }
}
#endif
