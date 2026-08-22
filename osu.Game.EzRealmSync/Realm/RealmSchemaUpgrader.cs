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
    /// 工具侧 schema 升级（同大版本）：在备份后的工作副本上走可控游戏 migration，禁止删库重建。
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
            if (kind is RealmDiskSchemaKind.Unknown)
                throw new InvalidOperationException($"无法识别的 Realm schema 版本 {sourceSchema}：{realmFilePath}");

            // 越界直接失败（低于同大版本下限 / 高于工具内置）
            RealmSchemaToolPolicy.EnsureCanOpen(sourceSchema.Value);

            int latestSupportedSchema = RealmSchemaToolPolicy.LatestSupportedForKind(kind);

            if (sourceSchema.Value == latestSupportedSchema)
            {
                if (canOpenWithoutMigration(realmFilePath, sourceSchema.Value))
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

                throw new RealmUserOperationException(
                    RealmUserErrorKind.SchemaModelMismatch,
                    $"Realm 磁盘 schema 已是本工具最新（{latestSupportedSchema}），但对象模型仍无法打开。升级无法修复此问题：请确认 EzRealmSync 与写出该库的 Ez2Lazer 使用同一线 osu.Game，或从完整备份恢复。文件：{realmFilePath}");
            }

            string fullPath = Path.GetFullPath(realmFilePath);
            string filename = Path.GetFileName(fullPath);
            string tempRoot = EzRealmSyncDataPaths.CreateTempSubdirectory("schema-upgrade");
            string workRealmPath = Path.Combine(tempRoot, filename);

            Directory.CreateDirectory(tempRoot);

            try
            {
                progress?.Report(new ScanProgress { Progress = 0.05, Message = "正在准备迁移工作副本…" });
                cancellationToken.ThrowIfCancellationRequested();

                var dynamicBefore = RealmDynamicObjectCounter.Capture(fullPath);
                File.Copy(fullPath, workRealmPath, overwrite: true);

                progress?.Report(new ScanProgress
                {
                    Progress = 0.2,
                    Message = $"正在迁移 schema（{sourceSchema} → {latestSupportedSchema}）…",
                });
                cancellationToken.ThrowIfCancellationRequested();

                // 仅在工作副本上 migration；失败不碰原文件
                using (var migrated = openWithMigrationForTool(workRealmPath, kind))
                {
                    migrated.Run(_ => { });
                }

                // 短命工具打开会跑 cleanupPendingDeletions；刷掉 SharedRealm 最终化，避免测试宿主退出时
                // realm-core 断言 !realm.is_in_transaction()。
                GC.Collect();
                GC.WaitForPendingFinalizers();

                int? upgradedSchema = RealmDiskSchemaReader.TryReadSchemaVersion(workRealmPath);
                if (upgradedSchema != latestSupportedSchema)
                {
                    throw new RealmUserOperationException(
                        RealmUserErrorKind.MigrationRequired,
                        $"迁移后磁盘 schema 为 {upgradedSchema?.ToString() ?? "未知"}，期望 {latestSupportedSchema}。已中止替换，请从备份恢复。");
                }

                if (!canOpenWithoutMigration(workRealmPath, latestSupportedSchema))
                {
                    throw new RealmUserOperationException(
                        RealmUserErrorKind.SchemaModelMismatch,
                        $"迁移后仍无法以当前模型 pinned 打开（schema {latestSupportedSchema}）。已中止替换，请从备份恢复。");
                }

                RealmMigrationCounts afterCounts;
                using (var access = RealmSchemaProbe.Open(workRealmPath, latestSupportedSchema))
                    afterCounts = RealmMigrationCounts.Capture(access);

                // 动态计数在 migration 前采集；迁移后 typed 计数不应灾难性低于动态基线
                if (dynamicBefore.Files > 0 && afterCounts.RealmFiles < dynamicBefore.Files * 0.99
                    || dynamicBefore.Rulesets > 0 && afterCounts.Rulesets < dynamicBefore.Rulesets
                    || dynamicBefore.Skins > 0 && afterCounts.Skins < dynamicBefore.Skins)
                {
                    throw new RealmUserOperationException(
                        RealmUserErrorKind.MigrationRequired,
                        $"迁移后数据量异常（磁盘前 {dynamicBefore}，迁移后 {afterCounts}）。已中止替换，请从备份恢复。");
                }

                progress?.Report(new ScanProgress { Progress = 0.9, Message = "正在替换原文件…" });
                cancellationToken.ThrowIfCancellationRequested();
                File.Copy(workRealmPath, fullPath, overwrite: true);

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
            catch (RealmUserOperationException ex) when (ex.Kind is RealmUserErrorKind.MigrationRequired or RealmUserErrorKind.LegacyReaderUnavailable)
            {
                return false;
            }
            catch (Exception ex) when (RealmOpenErrorClassifier.IsMigrationRequired(ex))
            {
                return false;
            }
        }

        internal static RealmAccess OpenWithMigrationForTool(string realmFilePath, RealmDiskSchemaKind kind) =>
            openWithMigrationForTool(realmFilePath, kind);

        private static RealmAccess openWithMigrationForTool(string realmFilePath, RealmDiskSchemaKind kind)
        {
            string fullPath = Path.GetFullPath(realmFilePath);
            string storageRoot = RealmWorkspacePaths.ResolveStorageRoot(fullPath);
            string filename = Path.GetFileName(fullPath);
            var storage = new NativeStorage(storageRoot);

            // 与 OpenWithMigrationForTool 等价：migration + 禁止降级删库；用公开构造以便当前 NuGet 也可编译。
            return kind == RealmDiskSchemaKind.EzExtended
                ? new RealmAccess(storage, filename, useDevelopmentVersionedFilenames: false, allowDestructiveRecoveryOnSchemaMismatch: false, performSchemaMigration: true)
                : new OfficialRealmAccess(storage, filename, allowDestructiveRecoveryOnSchemaMismatch: false);
        }
    }
}
#endif
