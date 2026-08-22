#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm.Readers;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 统一 Realm 访问策略：按操作意图（探测 / 只读 / 写回 / 修复 migration）分流。
    /// </summary>
    public static class RealmAccessGateway
    {
        /// <summary>只读文件头 schema，不打开库。</summary>
        public static int? ProbeSchema(string realmFilePath) =>
            RealmDiskSchemaReader.TryReadSchemaVersion(realmFilePath);

        public static int ResolveSchemaVersion(string realmFilePath, int? diskSchemaVersion) =>
            diskSchemaVersion ?? ProbeSchema(realmFilePath)
            ?? throw new InvalidOperationException($"无法读取 Realm schema 版本：{realmFilePath}");

        /// <summary>只读 Diff 快照：主 lib 进程内优先，legacy 时 ReadSidecar + readers 包。</summary>
        public static RealmDiffSnapshot ReadDiffSnapshot(
            string realmFilePath,
            int pinnedDiskSchemaVersion,
            IReadOnlyList<EntityKind>? entityKinds = null,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            RefreshReaders();
            return RealmDiffSnapshotProvider.Read(realmFilePath, pinnedDiskSchemaVersion, entityKinds, progress, cancellationToken);
        }

        public static bool RequiresSidecarForRead(string realmFilePath, int pinnedDiskSchemaVersion)
        {
            RefreshReaders();
            return RealmDiffSnapshotProvider.RequiresSidecarForRead(realmFilePath, pinnedDiskSchemaVersion);
        }

        public static RealmSyncApplyBundle ExportApplyBundleViaSidecar(
            string realmFilePath,
            int pinnedDiskSchemaVersion,
            IReadOnlyList<Guid> itemIds,
            CancellationToken cancellationToken = default)
        {
            RefreshReaders();
            return RealmDiffSnapshotProvider.ExportApplyBundleViaSidecar(realmFilePath, pinnedDiskSchemaVersion, itemIds, cancellationToken);
        }

        /// <summary>写回 / 删改 / 导入；legacy schema 失败时不走 Sidecar。</summary>
        public static RealmAccess OpenForMutation(string realmFilePath, int? diskSchemaVersion = null) =>
            OpenForWrite(realmFilePath, diskSchemaVersion);

        /// <summary>写回 / 删改 / 导入；legacy schema 失败时不走 Sidecar。</summary>
        public static RealmAccess OpenForWrite(string realmFilePath, int? diskSchemaVersion = null)
        {
            try
            {
                return openWithoutMigration(realmFilePath, diskSchemaVersion);
            }
            catch (RealmUserOperationException ex) when (ex.Kind is RealmUserErrorKind.MigrationRequired or RealmUserErrorKind.LegacyReaderUnavailable)
            {
                throw wrapMutationOpenFailure(ex);
            }
        }

        /// <summary>修复页 migration / 转官方等显式允许在工作副本上升 schema 的路径。</summary>
        public static RealmAccess OpenForMigration(string realmFilePath, int? diskSchemaVersion = null) =>
            openWithoutMigration(realmFilePath, diskSchemaVersion);

        /// <summary>进程内只读打开 current schema；legacy 返回 false。</summary>
        public static bool TryOpenInProcessForRead(string realmFilePath, int pinnedDiskSchemaVersion, out RealmAccess? access)
        {
            RefreshReaders();
            return RealmAccessOpenCore.TryOpenCurrentInProcess(realmFilePath, pinnedDiskSchemaVersion, out access);
        }

        public static void RefreshReaders() => RealmReaderRegistry.Instance.Refresh();

        private static RealmAccess openWithoutMigration(string realmFilePath, int? diskSchemaVersion) =>
            RealmSchemaProbe.Open(realmFilePath, diskSchemaVersion);

        private static RealmUserOperationException wrapMutationOpenFailure(RealmUserOperationException ex)
        {
            if (ex.Kind == RealmUserErrorKind.LegacyReaderUnavailable)
            {
                return new RealmUserOperationException(
                    RealmUserErrorKind.LegacyReaderUnavailable,
                    $"写操作无法用当前 lib 打开该 schema。请先在「修复」页升级或转官方，或确认工具与库版本一致。{ex.Detail}",
                    ex);
            }

            return new RealmUserOperationException(
                RealmUserErrorKind.MigrationRequired,
                $"写操作需要 lib 最新 schema 才能打开该库。请在「修复」页点击「升级到 lib 最新」。{ex.Detail}",
                ex);
        }
    }
}
#endif
