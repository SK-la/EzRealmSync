namespace osu.Game.EzRealmSync.Contracts
{
    /// <summary>ReadSidecar Worker 输入：按 reader lib pinned 只读 Diff 快照。</summary>
    public sealed class RealmReadJob
    {
        /// <summary>reader 包内 <c>lib/</c> 绝对路径。</summary>
        public required string ReaderLibDirectory { get; set; }

        public required string RealmFilePath { get; set; }

        public int PinnedDiskSchemaVersion { get; set; }

        /// <summary><c>official</c> 或 <c>ez</c>。</summary>
        public required string Profile { get; set; }

        /// <summary>可选实体类型过滤（BeatmapSet / Beatmap / Score / BeatmapCollection）；空表示全部。</summary>
        public List<string> EntityKinds { get; set; } = new List<string>();
    }

    public sealed class RealmReadResult
    {
        public bool Success { get; set; }

        public string? ErrorMessage { get; set; }

        public List<RealmDiffEntityDto> Entities { get; set; } = new List<RealmDiffEntityDto>();
    }

    /// <summary>与引擎 <c>RealmDiffEntity</c> 同形，供 sidecar JSON 往返。</summary>
    public sealed class RealmDiffEntityDto
    {
        public Guid Id { get; set; }

        public string EntityKind { get; set; } = string.Empty;

        public string Hash { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Artist { get; set; } = string.Empty;

        public string Ruleset { get; set; } = string.Empty;

        public DateTimeOffset? Date { get; set; }

        public long? OnlineId { get; set; }

        public string? DifficultyName { get; set; }

        public int? CollectionBeatmapCount { get; set; }

        public string? CollectionHashFingerprint { get; set; }
    }

    /// <summary>ReadSidecar Apply 导出：按 GUID 导出完整行供主进程写入目标库。</summary>
    public sealed class RealmApplyExportJob
    {
        public required string ReaderLibDirectory { get; set; }

        public required string SourceRealmFilePath { get; set; }

        public int PinnedDiskSchemaVersion { get; set; }

        public required string Profile { get; set; }

        public List<Guid> ItemIds { get; set; } = new List<Guid>();
    }

    public sealed class RealmApplyExportResult
    {
        public bool Success { get; set; }

        public string? ErrorMessage { get; set; }

        /// <summary>选中项的谱面集 / 难度 / 成绩 / 收藏夹 DTO（与转官方导出同形，便于复用插入逻辑）。</summary>
        public RealmSyncApplyBundle? Bundle { get; set; }
    }

    public sealed class RealmSyncApplyBundle
    {
        public List<OfficialBeatmapSetDto> BeatmapSets { get; set; } = new List<OfficialBeatmapSetDto>();

        public List<OfficialBeatmapDto> Beatmaps { get; set; } = new List<OfficialBeatmapDto>();

        public List<OfficialScoreDto> Scores { get; set; } = new List<OfficialScoreDto>();

        public List<OfficialCollectionDto> Collections { get; set; } = new List<OfficialCollectionDto>();
    }
}
