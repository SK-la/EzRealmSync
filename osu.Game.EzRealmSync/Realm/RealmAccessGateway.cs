#if HAS_EZ_OSU_GAME
using osu.Game.Database;
#endif
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm.Dynamic;
#if HAS_EZ_OSU_GAME
using osu.Game.EzRealmSync.Realm.Readers;
#endif

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 统一 Realm 访问策略：同步走 DynamicRealm；typed 打开仅用于修复/转官方/数据页。
    /// </summary>
    public static class RealmAccessGateway
    {
        /// <summary>只读文件头 schema，不打开库、不加载 osu.Game.dll。</summary>
        public static int? ProbeSchema(string realmFilePath) =>
            RealmDiskSchemaReader.TryReadSchemaVersion(realmFilePath);

        public static int ResolveSchemaVersion(string realmFilePath, int? diskSchemaVersion) =>
            diskSchemaVersion ?? ProbeSchema(realmFilePath)
            ?? throw new InvalidOperationException($"无法读取 Realm schema 版本：{realmFilePath}");

        /// <summary>只读 Diff 快照：官方基线 DynamicRealm，不加载 osu.Game.dll。</summary>
        public static RealmDiffSnapshot ReadDiffSnapshot(
            string realmFilePath,
            IReadOnlyList<EntityKind>? entityKinds = null,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return DynamicBaselineReader.ReadDiffSnapshot(realmFilePath, entityKinds, progress, cancellationToken);
        }
        /// <summary>数据 Tab 只读浏览：动态打开，按磁盘 schema 读，官方库与 Ez 库同一条路径。</summary>
        public static RealmSnapshot ReadBrowseSnapshot(
            RealmFileEntry file,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return RealmBrowseSnapshotProvider.Read(file, progress, cancellationToken);
        }

        /// <summary>
        /// 动态只读打开：按磁盘 schema 读，不迁移、不加载 osu.Game。顺手把该版本的 schema 快照落盘
        /// （「碰过哪个版本就有哪个版本的快照」）；快照失败不影响本次读取。
        /// </summary>
        public static DynamicRealmSession OpenDynamicForRead(string realmFilePath, out RealmSchemaSnapshot schema)
        {
            DynamicRealmSession session = DynamicRealmSession.OpenDynamic(realmFilePath, readOnly: true);

            try
            {
                RealmSchemaSnapshotStore.Default.TryCapture(session);
                schema = DynamicSchemaReader.Read(session);
                return session;
            }
            catch
            {
                session.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 动态可写打开：**不迁移**，只改磁盘上已有的官方基线列（Ez 列一律不动）。
        /// 调用方自带事务，并负责进程占用 / 冲突检查。
        /// </summary>
        public static DynamicRealmSession OpenDynamicForWrite(string realmFilePath, out RealmSchemaSnapshot schema)
        {
            DynamicRealmSession session = DynamicRealmSession.OpenDynamic(realmFilePath, readOnly: false);

            try
            {
                RealmSchemaSnapshotStore.Default.TryCapture(session);
                schema = DynamicSchemaReader.Read(session);
                return session;
            }
            catch
            {
                session.Dispose();
                throw;
            }
        }

#if HAS_EZ_OSU_GAME

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
                    $"缺少对应版本的 osu.Game.dll，无法打开这份 Realm 文件。{ex.Detail}",
                    ex);
            }

            return new RealmUserOperationException(
                RealmUserErrorKind.MigrationRequired,
                $"无法修改这份 Realm 文件：版本过旧，需先升级。{ex.Detail}",
                ex);
        }
#endif
    }
}
