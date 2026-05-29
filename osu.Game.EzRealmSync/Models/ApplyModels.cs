namespace osu.Game.EzRealmSync.Models
{
    public sealed class ApplyRequest
    {
        /// <summary>A→B 写入计划；设置后优先于 <see cref="Direction"/> / <see cref="Paths"/>。</summary>
        public RealmWritePlan? WritePlan { get; init; }

        public SyncDirection Direction { get; init; }

        public PathConfiguration Paths { get; init; } = new();

        public IReadOnlyList<Guid> ItemIds { get; init; } = Array.Empty<Guid>();

        public bool CreateBackup { get; init; } = true;

        /// <summary>写入前备份目标库；为空时使用 <see cref="EzRealmSyncDefaults.DefaultBackupDirectory"/>。</summary>
        public string? BackupDirectory { get; init; }

        public bool DeleteFromSource { get; init; }
    }

    public sealed class ApplyProgress
    {
        public double Progress { get; init; }

        public string Message { get; init; } = string.Empty;
    }

    public sealed class ApplyResult
    {
        public int AppliedCount { get; init; }

        public string? BackupPath { get; init; }
    }
}
