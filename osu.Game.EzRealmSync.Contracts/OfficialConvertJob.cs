namespace osu.Game.EzRealmSync.Contracts
{
    /// <summary>转官方写库 Worker 输入（JSON）。主进程从 Ez 源构建，不含 Ez 独有列。</summary>
    public sealed class OfficialConvertJob
    {
        public int TargetUpstreamSchema { get; set; }

        public required string TargetRealmPath { get; set; }

        public List<OfficialRulesetDto> Rulesets { get; set; } = new List<OfficialRulesetDto>();

        public List<OfficialBeatmapSetDto> BeatmapSets { get; set; } = new List<OfficialBeatmapSetDto>();

        public List<OfficialScoreDto> Scores { get; set; } = new List<OfficialScoreDto>();

        public List<OfficialCollectionDto> Collections { get; set; } = new List<OfficialCollectionDto>();

        public List<string> FileHashes { get; set; } = new List<string>();

        public List<OfficialSkinDto> Skins { get; set; } = new List<OfficialSkinDto>();

        public OfficialConvertFilterStats FilterStats { get; set; } = new OfficialConvertFilterStats();
    }

    public sealed class OfficialConvertFilterStats
    {
        public int SkippedSkins { get; set; }

        public int SkippedScores { get; set; }

        public int SkippedBeatmapSets { get; set; }

        public int SkippedRulesets { get; set; }

        public int PrunedCollectionEntries { get; set; }
    }

    public sealed class OfficialConvertResult
    {
        public bool Success { get; set; }

        public string? ErrorMessage { get; set; }

        public int AppliedCount { get; set; }

        public int RealmFileCount { get; set; }

        public int TargetSchemaVersion { get; set; }

        public OfficialConvertFilterStats? FilterStats { get; set; }
    }

    public sealed class OfficialRulesetDto
    {
        public required string ShortName { get; set; }

        public int OnlineID { get; set; }

        public string Name { get; set; } = string.Empty;

        public string InstantiationInfo { get; set; } = string.Empty;

        public int LastAppliedDifficultyVersion { get; set; }

        public bool Available { get; set; }
    }

    public sealed class OfficialBeatmapSetDto
    {
        public Guid ID { get; set; }

        public int OnlineID { get; set; }

        public DateTimeOffset DateAdded { get; set; }

        public DateTimeOffset? DateSubmitted { get; set; }

        public DateTimeOffset? DateRanked { get; set; }

        public int StatusInt { get; set; }

        public bool DeletePending { get; set; }

        public string Hash { get; set; } = string.Empty;

        public bool Protected { get; set; }

        public List<OfficialNamedFileDto> Files { get; set; } = new List<OfficialNamedFileDto>();

        public List<OfficialBeatmapDto> Beatmaps { get; set; } = new List<OfficialBeatmapDto>();
    }

    public sealed class OfficialBeatmapDto
    {
        public Guid ID { get; set; }

        public string DifficultyName { get; set; } = string.Empty;

        public required string RulesetShortName { get; set; }

        public OfficialBeatmapDifficultyDto Difficulty { get; set; } = new OfficialBeatmapDifficultyDto();

        public OfficialBeatmapMetadataDto Metadata { get; set; } = new OfficialBeatmapMetadataDto();

        public OfficialBeatmapUserSettingsDto UserSettings { get; set; } = new OfficialBeatmapUserSettingsDto();

        public int StatusInt { get; set; }

        public int OnlineID { get; set; }

        public double Length { get; set; }

        public double BPM { get; set; }

        public string Hash { get; set; } = string.Empty;

        public double StarRating { get; set; }

        public string MD5Hash { get; set; } = string.Empty;

        public string OnlineMD5Hash { get; set; } = string.Empty;

        public DateTimeOffset? LastLocalUpdate { get; set; }

        public DateTimeOffset? LastOnlineUpdate { get; set; }

        public bool Hidden { get; set; }

        public int EndTimeObjectCount { get; set; }

        public int TotalObjectCount { get; set; }

        public DateTimeOffset? LastPlayed { get; set; }

        public int BeatDivisor { get; set; }

        public double? EditorTimestamp { get; set; }
    }

    public sealed class OfficialBeatmapMetadataDto
    {
        public string Title { get; set; } = string.Empty;

        public string TitleUnicode { get; set; } = string.Empty;

        public string Artist { get; set; } = string.Empty;

        public string ArtistUnicode { get; set; } = string.Empty;

        public OfficialRealmUserDto Author { get; set; } = new OfficialRealmUserDto();

        public string Source { get; set; } = string.Empty;

        public string Tags { get; set; } = string.Empty;

        public List<string> UserTags { get; set; } = new List<string>();

        public int PreviewTime { get; set; }

        public string AudioFile { get; set; } = string.Empty;

        public string BackgroundFile { get; set; } = string.Empty;
    }

    public sealed class OfficialBeatmapDifficultyDto
    {
        public float DrainRate { get; set; } = 5;

        public float CircleSize { get; set; } = 5;

        public float OverallDifficulty { get; set; } = 5;

        public float ApproachRate { get; set; } = 5;

        public double SliderMultiplier { get; set; } = 1.4;

        public double SliderTickRate { get; set; } = 1;
    }

    public sealed class OfficialBeatmapUserSettingsDto
    {
        public double Offset { get; set; }
    }

    public sealed class OfficialRealmUserDto
    {
        public int OnlineID { get; set; } = 1;

        public string Username { get; set; } = string.Empty;

        public string CountryString { get; set; } = string.Empty;
    }

    public sealed class OfficialNamedFileDto
    {
        public required string Hash { get; set; }

        public required string Filename { get; set; }
    }

    public sealed class OfficialScoreDto
    {
        public Guid ID { get; set; }

        public string BeatmapHash { get; set; } = string.Empty;

        public required string RulesetShortName { get; set; }

        public string ClientVersion { get; set; } = string.Empty;

        public string Hash { get; set; } = string.Empty;

        public bool DeletePending { get; set; }

        public long TotalScore { get; set; }

        public long TotalScoreWithoutMods { get; set; }

        public int TotalScoreVersion { get; set; }

        public long? LegacyTotalScore { get; set; }

        public bool BackgroundReprocessingFailed { get; set; }

        public int MaxCombo { get; set; }

        public double Accuracy { get; set; }

        public DateTimeOffset Date { get; set; }

        public double? PP { get; set; }

        public long OnlineID { get; set; }

        public long LegacyOnlineID { get; set; }

        public OfficialRealmUserDto User { get; set; } = new OfficialRealmUserDto();

        public string ModsJson { get; set; } = string.Empty;

        public string StatisticsJson { get; set; } = string.Empty;

        public string MaximumStatisticsJson { get; set; } = string.Empty;

        public List<int> Pauses { get; set; } = new List<int>();

        public int RankInt { get; set; }

        public int Combo { get; set; }

        public bool IsLegacyScore { get; set; }

        public List<OfficialNamedFileDto> Files { get; set; } = new List<OfficialNamedFileDto>();
    }

    public sealed class OfficialCollectionDto
    {
        public Guid ID { get; set; }

        public string Name { get; set; } = string.Empty;

        public List<string> BeatmapMD5Hashes { get; set; } = new List<string>();

        public DateTimeOffset LastModified { get; set; }
    }

    public sealed class OfficialSkinDto
    {
        public Guid ID { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Creator { get; set; } = string.Empty;

        public string InstantiationInfo { get; set; } = string.Empty;

        public string Hash { get; set; } = string.Empty;

        public bool Protected { get; set; }

        public bool DeletePending { get; set; }

        public List<OfficialNamedFileDto> Files { get; set; } = new List<OfficialNamedFileDto>();
    }
}
