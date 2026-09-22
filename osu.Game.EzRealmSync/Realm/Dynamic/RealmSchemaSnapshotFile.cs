namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 一次 schema 采集的落盘单元：磁盘版本 + 全量类/列定义 + 采集来源。
    ///
    /// 磁盘版本即快照身份（同一版本只留一份），因此它同时决定文件名；
    /// <see cref="SourcePath"/> 只用于回答"这份快照是哪来的"，不参与任何判定。
    /// </summary>
    public sealed record RealmSchemaSnapshotFile(
        int DiskSchemaVersion,
        RealmSchemaSnapshot Snapshot,
        string? SourcePath,
        DateTimeOffset CollectedAtUtc);
}
