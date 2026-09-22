using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm.Dynamic;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 数据 Tab 只读浏览：一份库怎么读只取决于它自己——动态打开，按磁盘 schema 逐列取值。
    /// 官方库、Ez 旧库、当前 Ez 库走同一条路径，不再需要 reader 包或 Official Worker 代读。
    /// </summary>
    public static class RealmBrowseSnapshotProvider
    {
        public static RealmSnapshot Read(
            RealmFileEntry file,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(file);

            // 浏览是「完整访问一份库」的典型入口：顺手把它的 schema 落盘，碰过哪个版本就有哪个版本的快照。
            RealmSchemaSnapshotStore.Default.TryCapture(file.FilePath);

            using var session = DynamicRealmSession.OpenDynamic(file.FilePath, readOnly: true);

            EzRealmSyncLog.Info($"ReadBrowseSnapshot via dynamic open file={file.FilePath}");

            return DynamicBrowseSnapshotBuilder.Build(file, session, progress, cancellationToken);
        }
    }
}
