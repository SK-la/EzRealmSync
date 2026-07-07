#if HAS_EZ_OSU_GAME
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm.Readers;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 使用内置 osu.Game.dll 将 Realm 原地迁移到当前工具支持的最新 schema。
    /// </summary>
    public static class RealmSchemaUpgrader
    {
        public static RealmSchemaUpgradeResult UpgradeInPlace(
            string realmFilePath,
            int? knownDiskSchemaVersion = null,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
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

            progress?.Report(new ScanProgress { Progress = 0.2, Message = "正在迁移 schema…" });
            cancellationToken.ThrowIfCancellationRequested();

            using (var access = openWithMigration(realmFilePath, kind))
            {
                access.Run(_ => { });
            }

            int? upgradedSchema = RealmDiskSchemaReader.TryReadSchemaVersion(realmFilePath)
                                  ?? latestSupportedSchema;

            progress?.Report(new ScanProgress { Progress = 1, Message = "迁移完成" });

            return new RealmSchemaUpgradeResult
            {
                RealmFilePath = realmFilePath,
                SourceSchemaVersion = sourceSchema.Value,
                TargetSchemaVersion = upgradedSchema.Value,
                AlreadyUpToDate = sourceSchema.Value == upgradedSchema.Value,
            };
        }

        private static bool canOpenWithoutMigration(string realmFilePath, RealmDiskSchemaKind kind, int diskSchemaVersion)
        {
            try
            {
                using var access = kind == RealmDiskSchemaKind.EzExtended
                    ? RealmDiffReader.OpenEzRealm(realmFilePath, diskSchemaVersion)
                    : RealmDiffReader.OpenOfficialRealm(realmFilePath, diskSchemaVersion);
                access.Run(_ => { });
                return true;
            }
            catch (Exception ex) when (RealmOpenErrorClassifier.IsMigrationRequired(ex))
            {
                return false;
            }
        }

        private static RealmAccess openWithMigration(string realmFilePath, RealmDiskSchemaKind kind)
        {
            string fullPath = Path.GetFullPath(realmFilePath);
            string storageRoot = RealmWorkspacePaths.ResolveStorageRoot(fullPath);
            string filename = Path.GetFileName(fullPath);
            var storage = new NativeStorage(storageRoot);

            return kind == RealmDiskSchemaKind.EzExtended
                ? new RealmAccess(
                    storage,
                    filename,
                    useDevelopmentVersionedFilenames: false,
                    allowDestructiveRecoveryOnSchemaMismatch: false,
                    performSchemaMigration: true)
                : new OfficialRealmAccess(storage, filename, allowDestructiveRecoveryOnSchemaMismatch: false);
        }
    }
}
#endif
