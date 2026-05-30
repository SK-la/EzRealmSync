namespace osu.Game.EzRealmSync.Models
{
    public enum ExportDataKind
    {
        BeatmapSet,
        Beatmap,
        Collection,
        Score,
    }

    public sealed class RealmExportItem
    {
        public Guid Id { get; init; }

        public string Title { get; init; } = string.Empty;

        public string Artist { get; init; } = string.Empty;

        /// <summary>files/ 内源文件相对路径（hash 分片路径）。</summary>
        public string RelativePath { get; init; } = string.Empty;

        /// <summary>输出目录内相对路径；为空时与 <see cref="RelativePath"/> 相同。</summary>
        public string? DestinationRelativePath { get; init; }

        public string? CollectionName { get; init; }
    }

    public sealed class RealmExportCatalog
    {
        public ExportDataKind Kind { get; init; }

        public IReadOnlyList<RealmExportItem> Items { get; init; } = Array.Empty<RealmExportItem>();
    }

    public sealed class RealmExportRequest
    {
        public required string RealmId { get; init; }

        public ExportDataKind Kind { get; init; }

        public required IReadOnlyList<Guid> ItemIds { get; init; }

        public required string OutputDirectory { get; init; }

        /// <summary>为空时按种类使用 <c>songs-</c> 或 <c>replays-</c> 加时间戳。</summary>
        public string? FolderName { get; init; }

        public required string FilesDirectory { get; init; }
    }

    public sealed class RealmExportResult
    {
        public string OutputRoot { get; init; } = string.Empty;

        public int ExportedCount { get; init; }

        public int SkippedCount { get; init; }
    }
}
