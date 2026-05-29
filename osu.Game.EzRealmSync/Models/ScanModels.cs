namespace osu.Game.EzRealmSync.Models
{
    public sealed class ScanRequest
    {
        public RealmWritePlan? WritePlan { get; init; }

        public SyncDirection Direction { get; init; }

        public PathConfiguration Paths { get; init; } = new();

        public IReadOnlyList<EntityKind> EntityKinds { get; init; } = Array.Empty<EntityKind>();
    }

    public sealed class ScanProgress
    {
        public double Progress { get; init; }

        public string Message { get; init; } = string.Empty;
    }

    public sealed class ScanResult
    {
        public IReadOnlyList<DiffItem> SourceOnly { get; init; } = Array.Empty<DiffItem>();

        public IReadOnlyList<DiffItem> TargetOnly { get; init; } = Array.Empty<DiffItem>();

        public IReadOnlyList<DiffItem> Conflicted { get; init; } = Array.Empty<DiffItem>();
    }
}
