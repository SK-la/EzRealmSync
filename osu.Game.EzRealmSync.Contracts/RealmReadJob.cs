namespace osu.Game.EzRealmSync.Contracts
{
    /// <summary>OfficialWrite Worker 输入：以 pinned disk schema 只读 Diff 快照。</summary>
    public sealed class RealmReadJob
    {
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

    /// <summary>同步写入包：由 <c>DynamicBaselineReader</c> 从源库导出、<c>DynamicBaselineWriter</c> 写入目标库。</summary>
    public sealed class RealmSyncApplyBundle
    {
        public List<OfficialBeatmapSetDto> BeatmapSets { get; set; } = new List<OfficialBeatmapSetDto>();

        public List<OfficialBeatmapDto> Beatmaps { get; set; } = new List<OfficialBeatmapDto>();

        public List<OfficialScoreDto> Scores { get; set; } = new List<OfficialScoreDto>();

        public List<OfficialCollectionDto> Collections { get; set; } = new List<OfficialCollectionDto>();

        public List<OfficialSkinDto> Skins { get; set; } = new List<OfficialSkinDto>();
    }
}
