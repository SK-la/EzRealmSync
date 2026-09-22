#if HAS_EZ_OSU_GAME
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.IO;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 「升级 Realm 文件」：把 Ez 库升到当前 Ez 模型的 schema，必然要用 Ez 模型镜像 schema，
    /// 因此留在 <c>HAS_EZ_OSU_GAME</c> 下；计划中的后续步骤会整条功能下线。
    /// 「转回官方版」已是纯动态实现，见 <c>RealmRealmDataService.ConvertOfficial.cs</c>。
    /// </summary>
    public sealed partial class RealmRealmDataService
    {
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
    }
}
#endif
