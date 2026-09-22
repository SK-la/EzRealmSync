using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm.Dynamic;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 统一 Realm 访问策略：全部走 DynamicRealm，不加载 osu.Game.dll。
    /// 打开一律按磁盘 schema 读，不迁移、不改版本号。
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
    }
}
