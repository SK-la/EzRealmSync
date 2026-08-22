#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 转官方读源：优先 pinned 打开；legacy 时在 backup 副本上 Ez migration 后读取（不修改原文件）。
    /// </summary>
    public sealed class RealmOfficialConvertSourceOpener : IDisposable
    {
        private readonly RealmAccess access;
        private readonly string? tempRoot;

        private RealmOfficialConvertSourceOpener(RealmAccess access, int actualReadSchema, string? tempRoot)
        {
            this.access = access;
            ActualReadSchema = actualReadSchema;
            this.tempRoot = tempRoot;
        }

        public RealmAccess Access => access;

        public int ActualReadSchema { get; }

        public static RealmOfficialConvertSourceOpener Open(
            string sourcePath,
            int sourceSchema,
            string backupPath,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            try
            {
                var direct = RealmAccessGateway.OpenForMutation(sourcePath, sourceSchema);
                return new RealmOfficialConvertSourceOpener(direct, sourceSchema, tempRoot: null);
            }
            catch (RealmUserOperationException ex) when (ex.Kind is RealmUserErrorKind.MigrationRequired or RealmUserErrorKind.LegacyReaderUnavailable)
            {
                return openViaMigratedWorkCopy(sourcePath, backupPath, progress, cancellationToken);
            }
        }

        private static RealmOfficialConvertSourceOpener openViaMigratedWorkCopy(
            string sourcePath,
            string backupPath,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            progress?.Report(new ScanProgress
            {
                Progress = 0.15,
                Message = "无法在 lib 模型下直接打开 legacy schema，正在临时迁移副本以读取…",
            });

            cancellationToken.ThrowIfCancellationRequested();

            string sourceName = Path.GetFileName(sourcePath);
            string tempRoot = EzRealmSyncDataPaths.CreateTempSubdirectory("official-convert-read");
            string workPath = Path.Combine(tempRoot, sourceName);
            Directory.CreateDirectory(tempRoot);

            File.Copy(backupPath, workPath, overwrite: true);

            using (var migrated = RealmSchemaUpgrader.OpenWithMigrationForTool(workPath, RealmDiskSchemaKind.EzExtended))
                migrated.Run(_ => { });

            GC.Collect();
            GC.WaitForPendingFinalizers();

            int latestEz = RealmSchemaToolPolicy.MaxSupportedEzFileSchema;
            var access = RealmAccessGateway.OpenForMigration(workPath, latestEz);
            return new RealmOfficialConvertSourceOpener(access, latestEz, tempRoot);
        }

        public void Dispose()
        {
            access.Dispose();

            if (tempRoot != null && Directory.Exists(tempRoot))
            {
                try
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
                catch
                {
                    // 临时目录清理失败不影响转换结果。
                }
            }
        }
    }
}
#endif
