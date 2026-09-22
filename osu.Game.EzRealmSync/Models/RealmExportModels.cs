namespace osu.Game.EzRealmSync.Models
{
    public enum ExportDataKind
    {
        BeatmapSet,
        Beatmap,
        Collection,
        Score,
        CollectionDb,
        /// <summary>写出 osu!stable <c>scores.db</c>（成绩元数据，不含 .osr 文件）。</summary>
        ScoreDb,
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

        /// <summary>收藏夹内谱面数量（仅收藏夹谱面 / 合集列表项）。</summary>
        public int BeatmapCount { get; init; }

        /// <summary>成绩玩家名（仅成绩项）。</summary>
        public string? PlayerName { get; init; }
    }

    public sealed class RealmExportCatalog
    {
        public ExportDataKind Kind { get; init; }

        public IReadOnlyList<RealmExportItem> Items { get; init; } = Array.Empty<RealmExportItem>();
    }

    /// <summary>
    /// 单个文件的导出对（源相对路径 → 目标相对路径）。
    ///
    /// 不依赖 osu.Game 模型：动态读路径与测试侧的 typed 参考实现共用同一类型，
    /// 对照测试才能直接比两个列表。
    /// </summary>
    public readonly struct RealmExportFileEntry
    {
        public string SourceRelative { get; init; }

        public string DestinationRelative { get; init; }

        /// <summary>收藏夹展开时的子目录名；为空表示直接落在输出根目录。</summary>
        public string? CollectionFolder { get; init; }
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

        /// <summary>批量导出成绩时按玩家名分子目录（<c>replays/玩家/</c>）。</summary>
        public bool GroupScoresByPlayer { get; init; } = true;
    }

    public sealed class RealmExportResult
    {
        public string OutputRoot { get; init; } = string.Empty;

        public int ExportedCount { get; init; }

        public int SkippedCount { get; init; }
    }
}
