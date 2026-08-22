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

        /// <summary>只读 Diff 快照：官方 Official Worker；Ez current 进程内；Ez legacy Sidecar。</summary>
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

        /// <summary>数据 Tab 只读浏览：官方 Official Worker；Ez current 进程内；Ez legacy Sidecar。</summary>
        public static RealmSnapshot ReadBrowseSnapshot(
            RealmFileEntry file,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            RefreshReaders();
            return RealmBrowseSnapshotProvider.Read(file, progress, cancellationToken);
        }

        /// <summary>是否需要子进程只读（官方或 Ez legacy）。</summary>
        public static bool RequiresSidecarForRead(string realmFilePath, int pinnedDiskSchemaVersion)
        {
            RefreshReaders();
            return RealmDiffSnapshotProvider.RequiresSidecarForRead(realmFilePath, pinnedDiskSchemaVersion);
        }

        /// <summary>子进程导出 Apply 包：官方 Official Worker；Ez legacy Sidecar。</summary>
        public static RealmSyncApplyBundle ExportApplyBundleViaSidecar(
            string realmFilePath,
            int pinnedDiskSchemaVersion,
            IReadOnlyList<Guid> itemIds,
            CancellationToken cancellationToken = default)
        {
            RefreshReaders();
            return RealmDiffSnapshotProvider.ExportApplyBundleViaSidecar(realmFilePath, pinnedDiskSchemaVersion, itemIds, cancellationToken);
        }

        /// <summary>官方目标库 Apply 写入（Official Worker）。</summary>
        public static OfficialApplyImportResult ApplyImportToOfficial(
            string targetRealmPath,
            int pinnedDiskSchemaVersion,
            IReadOnlyList<Guid> itemIds,
            RealmSyncApplyBundle bundle,
            CancellationToken cancellationToken = default)
        {
            var job = new OfficialApplyImportJob
            {
                TargetRealmPath = Path.GetFullPath(targetRealmPath),
                PinnedDiskSchemaVersion = pinnedDiskSchemaVersion,
                ItemIds = itemIds.ToList(),
                Bundle = bundle,
            };

            return OfficialReadProcessRunner.ApplyImport(job, cancellationToken);
        }

        /// <summary>写回 / 删改 / 导入；legacy schema 失败时不走 Sidecar。</summary>
        public static RealmAccess OpenForMutation(string realmFilePath, int? diskSchemaVersion = null) =>
            OpenForWrite(realmFilePath, diskSchemaVersion);

        /// <summary>写回 / 删改 / 导入；仅 Ez 库。官方库禁止主进程 Ez 模型打开。</summary>
        public static RealmAccess OpenForWrite(string realmFilePath, int? diskSchemaVersion = null)
        {
            int schema = ResolveSchemaVersion(realmFilePath, diskSchemaVersion);

            if (RealmSchemaSafety.IsOfficialDiskSchema(schema))
            {
                throw new RealmUserOperationException(
                    RealmUserErrorKind.SchemaModelMismatch,
                    $"官方库（schema {schema}）不得用主进程 Ez 模型写回。同步写入官方请走 Official Worker；数据 Tab 删改仅支持 Ez 库。文件：{realmFilePath}");
            }

            try
            {
                return openWithoutMigration(realmFilePath, schema);
            }
            catch (RealmUserOperationException ex) when (ex.Kind is RealmUserErrorKind.MigrationRequired or RealmUserErrorKind.LegacyReaderUnavailable)
            {
                throw wrapMutationOpenFailure(ex);
            }
        }

        /// <summary>修复页 migration；仅 Ez。官方 schema 升级请用官方客户端。</summary>
        public static RealmAccess OpenForMigration(string realmFilePath, int? diskSchemaVersion = null)
        {
            int schema = ResolveSchemaVersion(realmFilePath, diskSchemaVersion);

            if (RealmSchemaSafety.IsOfficialDiskSchema(schema))
            {
                throw new RealmUserOperationException(
                    RealmUserErrorKind.SchemaModelMismatch,
                    $"官方库（schema {schema}）不得经 OfficialRealmAccess migration（会写入 Ez 列）。请用官方 osu!lazer 升级，或从 Ez「转回官方版」。文件：{realmFilePath}");
            }

            return openWithoutMigration(realmFilePath, schema);
        }

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
