using osu.Game.EzRealmSync.Errors;

namespace osu.Game.EzRealmSync.Realm.Dynamic
{
    /// <summary>
    /// 「收窄成官方 N」的目标 schema 事实来源：某个**真实官方库**的 schema 快照。
    ///
    /// 为什么不按官方号手写清单：官方每个 upstream 增删的表与列必须逐列命中——少一列，官方客户端
    /// 打开时要做一次迁移；多一列，官方客户端根本不认。快照来自真实库，因此天然逐列对齐，
    /// 且版本比本工具已知的更新时同样成立（碰过哪个版本就有哪个版本的快照）。
    /// </summary>
    public sealed record OfficialSchemaSource(int UpstreamVersion, RealmSchemaSnapshot Snapshot, string Description);

    /// <summary>解析「官方 N 的 schema 从哪来」：优先本工具已采集的快照，其次由用户指定一份官方库当场采集。</summary>
    public static class OfficialSchemaSourceResolver
    {
        /// <summary>快照仓库里已经有官方 N 的 schema 时直接可用；没有时返回可操作的原因。</summary>
        public static bool TryFromSnapshots(int upstreamVersion, out OfficialSchemaSource? source, out string? error)
        {
            source = null;
            error = null;

            var store = RealmSchemaSnapshotStore.Default;

            if (store.TryLoadOfficialBaseline(upstreamVersion, out RealmSchemaSnapshot? snapshot, out int upstream))
            {
                source = new OfficialSchemaSource(upstream, snapshot!, $"本工具已采集的官方 {upstream} schema 快照");
                return true;
            }

            error = $"本工具还没有官方 {upstreamVersion} 的 schema 快照。请选择一份官方 {upstreamVersion} 的 client.realm"
                    + $"（或用官方 {upstreamVersion} 客户端在独立数据目录跑一次生成空库），本工具会从它采集快照。";

            return false;
        }

        /// <summary>
        /// 用户指定的官方库：显式采集快照（失败要如实报错，这条路径不是「顺手采集」），并要求它就是目标 upstream——
        /// 官方 51 的 schema 拿去收窄 52 的库会丢掉 52 才有的表列，不算转换成功。
        /// </summary>
        public static OfficialSchemaSource FromFile(string realmFilePath, int expectedUpstream)
        {
            RealmSchemaSnapshotFile file = RealmSchemaSnapshotStore.Default.Capture(realmFilePath);

            if (!RealmSchemaSnapshotClassifier.TryGetOfficialUpstream(file.DiskSchemaVersion, file.Snapshot, out int upstream))
            {
                throw new RealmUserOperationException(
                    RealmUserErrorKind.SchemaModelMismatch,
                    $"所选文件不是官方库（版本 {file.DiskSchemaVersion}，含 Ez 表或 Ez 列），不能当作官方 schema 来源：{realmFilePath}");
            }

            if (upstream != expectedUpstream)
            {
                throw new RealmUserOperationException(
                    RealmUserErrorKind.SchemaModelMismatch,
                    $"所选文件是官方 {upstream}，而这份 Ez 库对应官方 {expectedUpstream}。请选择官方 {expectedUpstream} 的库。");
            }

            return new OfficialSchemaSource(upstream, file.Snapshot, $"所选官方库 {Path.GetFileName(realmFilePath)}（官方 {upstream}）");
        }
    }
}
