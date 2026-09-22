namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 同步只认官方基线表/列名（与 OfficialSchema V51 对齐）。
    /// 运行时不得加载 osu.Game.dll；目标多出来的 Ez 列不写入。
    /// </summary>
    public static class OfficialBaselineSchema
    {
        public const string BeatmapSet = "BeatmapSet";
        public const string Beatmap = "Beatmap";
        public const string BeatmapMetadata = "BeatmapMetadata";
        public const string BeatmapDifficulty = "BeatmapDifficulty";
        public const string BeatmapUserSettings = "BeatmapUserSettings";
        public const string RealmUser = "RealmUser";
        public const string BeatmapCollection = "BeatmapCollection";
        public const string Skin = "Skin";
        public const string File = "File";
        public const string NamedFileUsage = "RealmNamedFileUsage";
        public const string Ruleset = "Ruleset";
        public const string Score = "Score";

        public static readonly IReadOnlyList<string> SyncTables =
        [
            BeatmapSet,
            Beatmap,
            BeatmapCollection,
            Skin,
            File,
            Ruleset,
            Score,
        ];

        public static readonly IReadOnlyList<string> EzOnlyPropertyNames =
        [
            "XxyStarRating",
            "PerformancePoints",
            "HasVideo",
            "HasStoryboard",
            "HostingKind",
            "ExternalContentRoot",
            "LastAppliedXxySrVersion",
            "ManiaHitMode",
            "ManiaHealthMode",
        ];

        private static readonly Guid ez2_skin_id = new Guid("fc372386-381d-4f8e-897a-c1d89ef39f9c");
        private static readonly Guid ez_style_pro_skin_id = new Guid("1E70839C-C0D8-4DBF-B747-0C08C89D412B");
        private static readonly Guid sbi_skin_id = new Guid("fc372386-381d-4f8e-897a-c1d89ef39f2c");

        private static readonly HashSet<string> ez_only_rulesets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "diva",
            "bms",
        };

        public static bool IsEzOnlyRuleset(string? shortName) =>
            !string.IsNullOrWhiteSpace(shortName) && ez_only_rulesets.Contains(shortName);

        public static bool IsEzOnlySkin(Guid id, string? instantiationInfo, string? name)
        {
            if (id == ez2_skin_id || id == ez_style_pro_skin_id || id == sbi_skin_id)
                return true;

            if (!string.IsNullOrWhiteSpace(instantiationInfo)
                && (instantiationInfo.Contains("Ez2Skin", StringComparison.OrdinalIgnoreCase)
                    || instantiationInfo.Contains("EzStyleProSkin", StringComparison.OrdinalIgnoreCase)
                    || instantiationInfo.Contains("SbISkin", StringComparison.OrdinalIgnoreCase)
                    || instantiationInfo.Contains("ScriptedSkin", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(name)
                   && (name.Contains("Ez2", StringComparison.OrdinalIgnoreCase)
                       || name.Contains("EzStylePro", StringComparison.OrdinalIgnoreCase)
                       || name.Contains("ScriptedSkin", StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsKnownProperty(string className, string propertyName) =>
            KnownProperties.TryGetValue(className, out var known) && known.Contains(propertyName);

        public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> KnownProperties =
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                [BeatmapSet] = names("ID", "OnlineID", "DateAdded", "DateSubmitted", "DateRanked", "Beatmaps", "Files", "Status", "DeletePending", "Hash", "Protected"),
                [Beatmap] = names(
                    "ID", "DifficultyName", "Ruleset", "Difficulty", "Metadata", "UserSettings", "BeatmapSet",
                    "Status", "OnlineID", "Length", "BPM", "Hash", "StarRating", "MD5Hash", "OnlineMD5Hash",
                    "LastLocalUpdate", "LastOnlineUpdate", "Hidden", "EndTimeObjectCount", "TotalObjectCount",
                    "LastPlayed", "BeatDivisor", "EditorTimestamp"),
                [BeatmapMetadata] = names(
                    "Title", "TitleUnicode", "Artist", "ArtistUnicode", "Author", "Source", "Tags", "UserTags",
                    "PreviewTime", "AudioFile", "BackgroundFile"),
                [BeatmapDifficulty] = names("DrainRate", "CircleSize", "OverallDifficulty", "ApproachRate", "SliderMultiplier", "SliderTickRate"),
                [BeatmapUserSettings] = names("Offset"),
                [RealmUser] = names("OnlineID", "Username", "CountryCode"),
                [BeatmapCollection] = names("ID", "Name", "BeatmapMD5Hashes", "LastModified"),
                [Skin] = names("ID", "Name", "Creator", "InstantiationInfo", "Hash", "Protected", "Files", "DeletePending"),
                [File] = names("Hash"),
                [NamedFileUsage] = names("File", "Filename"),
                [Ruleset] = names("ShortName", "OnlineID", "Name", "InstantiationInfo", "LastAppliedDifficultyVersion", "Available"),
                [Score] = names(
                    "ID", "BeatmapInfo", "ClientVersion", "BeatmapHash", "Ruleset", "Files", "Hash", "DeletePending",
                    "TotalScore", "TotalScoreWithoutMods", "TotalScoreVersion", "LegacyTotalScore",
                    "BackgroundReprocessingFailed", "MaxCombo", "Accuracy", "Date", "PP", "OnlineID", "LegacyOnlineID",
                    "User", "Mods", "Statistics", "MaximumStatistics", "Pauses", "Rank", "Combo", "IsLegacyScore"),
            };

        private static HashSet<string> names(params string[] values) => new HashSet<string>(values, StringComparer.Ordinal);
    }
}
