using Realms;

namespace osu.Game.EzRealmSync.OfficialSchema.V51
{
    [MapTo("Ruleset")]
    public class RulesetInfo : RealmObject
    {
        [PrimaryKey]
        public string ShortName { get; set; } = string.Empty;

        [Indexed]
        public int OnlineID { get; set; } = -1;

        public string Name { get; set; } = string.Empty;

        public string InstantiationInfo { get; set; } = string.Empty;

        public int LastAppliedDifficultyVersion { get; set; }

        public bool Available { get; set; }
    }

    [MapTo("BeatmapSet")]
    public class BeatmapSetInfo : RealmObject
    {
        [PrimaryKey]
        public Guid ID { get; set; }

        [Indexed]
        public int OnlineID { get; set; } = -1;

        public DateTimeOffset DateAdded { get; set; }

        public DateTimeOffset? DateSubmitted { get; set; }

        public DateTimeOffset? DateRanked { get; set; }

        public IList<BeatmapInfo> Beatmaps { get; } = null!;

        public IList<RealmNamedFileUsage> Files { get; } = null!;

        [MapTo("Status")]
        public int StatusInt { get; set; }

        public bool DeletePending { get; set; }

        public string Hash { get; set; } = string.Empty;

        public bool Protected { get; set; }
    }

    [MapTo("Beatmap")]
    public class BeatmapInfo : RealmObject
    {
        [PrimaryKey]
        public Guid ID { get; set; }

        public string DifficultyName { get; set; } = string.Empty;

        public RulesetInfo Ruleset { get; set; } = null!;

        public BeatmapDifficulty Difficulty { get; set; } = null!;

        public BeatmapMetadata Metadata { get; set; } = null!;

        public BeatmapUserSettings UserSettings { get; set; } = null!;

        public BeatmapSetInfo? BeatmapSet { get; set; }

        [MapTo("Status")]
        public int StatusInt { get; set; }

        [Indexed]
        public int OnlineID { get; set; } = -1;

        public double Length { get; set; }

        public double BPM { get; set; }

        public string Hash { get; set; } = string.Empty;

        public double StarRating { get; set; } = -1;

        [Indexed]
        public string MD5Hash { get; set; } = string.Empty;

        public string OnlineMD5Hash { get; set; } = string.Empty;

        public DateTimeOffset? LastLocalUpdate { get; set; }

        public DateTimeOffset? LastOnlineUpdate { get; set; }

        public bool Hidden { get; set; }

        public int EndTimeObjectCount { get; set; } = -1;

        public int TotalObjectCount { get; set; } = -1;

        public DateTimeOffset? LastPlayed { get; set; }

        public int BeatDivisor { get; set; } = 4;

        public double? EditorTimestamp { get; set; }
    }

    [MapTo("BeatmapMetadata")]
    public class BeatmapMetadata : RealmObject
    {
        public string Title { get; set; } = string.Empty;

        public string TitleUnicode { get; set; } = string.Empty;

        public string Artist { get; set; } = string.Empty;

        public string ArtistUnicode { get; set; } = string.Empty;

        public RealmUser Author { get; set; } = null!;

        public string Source { get; set; } = string.Empty;

        public string Tags { get; set; } = string.Empty;

        public IList<string> UserTags { get; } = null!;

        public int PreviewTime { get; set; } = -1;

        public string AudioFile { get; set; } = string.Empty;

        public string BackgroundFile { get; set; } = string.Empty;
    }

    [MapTo("BeatmapDifficulty")]
    public class BeatmapDifficulty : EmbeddedObject
    {
        public float DrainRate { get; set; } = 5;

        public float CircleSize { get; set; } = 5;

        public float OverallDifficulty { get; set; } = 5;

        public float ApproachRate { get; set; } = 5;

        public double SliderMultiplier { get; set; } = 1.4;

        public double SliderTickRate { get; set; } = 1;
    }

    public class BeatmapUserSettings : EmbeddedObject
    {
        public double Offset { get; set; }
    }

    public class RealmUser : EmbeddedObject
    {
        public int OnlineID { get; set; } = 1;

        public string Username { get; set; } = string.Empty;

        [MapTo("CountryCode")]
        public string CountryString { get; set; } = string.Empty;
    }

    [MapTo("Score")]
    public class ScoreInfo : RealmObject
    {
        [PrimaryKey]
        public Guid ID { get; set; }

        public BeatmapInfo? BeatmapInfo { get; set; }

        public string ClientVersion { get; set; } = string.Empty;

        [Indexed]
        public string BeatmapHash { get; set; } = string.Empty;

        public RulesetInfo Ruleset { get; set; } = null!;

        public IList<RealmNamedFileUsage> Files { get; } = null!;

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

        [Indexed]
        public long OnlineID { get; set; } = -1;

        [Indexed]
        public long LegacyOnlineID { get; set; } = -1;

        [MapTo("User")]
        public RealmUser RealmUser { get; set; } = null!;

        [MapTo("Mods")]
        public string ModsJson { get; set; } = string.Empty;

        [MapTo("Statistics")]
        public string StatisticsJson { get; set; } = string.Empty;

        [MapTo("MaximumStatistics")]
        public string MaximumStatisticsJson { get; set; } = string.Empty;

        public IList<int> Pauses { get; } = null!;

        [MapTo("Rank")]
        public int RankInt { get; set; }

        public int Combo { get; set; }

        public bool IsLegacyScore { get; set; }
    }

    public class BeatmapCollection : RealmObject
    {
        [PrimaryKey]
        public Guid ID { get; set; }

        public string Name { get; set; } = string.Empty;

        public IList<string> BeatmapMD5Hashes { get; } = null!;

        public DateTimeOffset LastModified { get; set; }
    }

    [MapTo("File")]
    public class RealmFile : RealmObject
    {
        [PrimaryKey]
        public string Hash { get; set; } = string.Empty;
    }

    public class RealmNamedFileUsage : EmbeddedObject
    {
        public RealmFile File { get; set; } = null!;

        public string Filename { get; set; } = null!;
    }

    [MapTo("Skin")]
    public class SkinInfo : RealmObject
    {
        [PrimaryKey]
        public Guid ID { get; set; }

        public string Name { get; set; } = null!;

        public string Creator { get; set; } = null!;

        public string InstantiationInfo { get; set; } = null!;

        public string Hash { get; set; } = string.Empty;

        public bool Protected { get; set; }

        public IList<RealmNamedFileUsage> Files { get; } = null!;

        public bool DeletePending { get; set; }
    }
}
