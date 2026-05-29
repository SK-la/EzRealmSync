namespace osu.Game.EzRealmSync.Models
{
    public enum ExportDataKind
    {
        BeatmapSet,
        Beatmap,
        Collection,
    }

    public sealed class RealmExportItem
    {
        public Guid Id { get; init; }

        public string Title { get; init; } = string.Empty;

        public string Artist { get; init; } = string.Empty;

        /// <summary>谱包内相对路径（如 <c>Artist - Title/song.osu</c>）。</summary>
        public string RelativePath { get; init; } = string.Empty;

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

        /// <summary>为空时使用 <c>songs-yyyyMMdd_HHmmss</c>。</summary>
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
