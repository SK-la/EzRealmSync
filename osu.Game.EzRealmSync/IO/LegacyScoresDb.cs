using System.Text;

namespace osu.Game.EzRealmSync.IO
{
    /// <summary>
    /// osu!stable <c>scores.db</c> 编解码（见 ppy wiki Legacy database file structure）。
    /// </summary>
    /// <remarks>
    /// TODO(legacy-db-merge): 支持把选中成绩合并进已有 scores.db（按谱面 MD5 分组追加/去重），
    /// 以及把收藏夹合并进已有 collection.db（见 <see cref="LegacyCollectionDb"/>）。
    /// </remarks>
    public static class LegacyScoresDb
    {
        public const string StableFileName = "scores.db";

        /// <summary>写出时使用的版本号；stable 会忽略未知版本并照常读取条目。</summary>
        public const int DefaultVersion = 20250108;

        private const byte string_flag = 0x0b;

        public static bool IsScoresDbFileName(string? pathOrName)
        {
            if (string.IsNullOrWhiteSpace(pathOrName))
                return false;

            return Path.GetFileName(pathOrName.Trim()).Equals(StableFileName, StringComparison.OrdinalIgnoreCase);
        }

        public static string ResolveOutputFile(string outputDirectory, string? folderOrFileName)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new ArgumentException("输出目录不能为空。", nameof(outputDirectory));

            string trimmed = folderOrFileName?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(trimmed))
            {
                string folder = $"scores-{DateTime.Now:yyyyMMdd_HHmmss}";
                return Path.Combine(outputDirectory, folder, StableFileName);
            }

            if (IsScoresDbFileName(trimmed) || trimmed.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                return Path.Combine(outputDirectory, Path.GetFileName(trimmed));

            return Path.Combine(outputDirectory, trimmed, StableFileName);
        }

        public static void WriteFile(string path, IEnumerable<LegacyScoresDbBeatmapGroup> beatmaps, int version = DefaultVersion)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using var stream = File.Create(path);
            Write(stream, beatmaps, version);
        }

        public static void Write(Stream stream, IEnumerable<LegacyScoresDbBeatmapGroup> beatmaps, int version = DefaultVersion)
        {
            var list = beatmaps as IList<LegacyScoresDbBeatmapGroup> ?? beatmaps.ToList();

            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(version);
            writer.Write(list.Count);

            foreach (var group in list)
            {
                writeOsuString(writer, group.BeatmapMd5);
                writer.Write(group.Scores.Count);

                foreach (var score in group.Scores)
                    writeScore(writer, score);
            }

            writer.Flush();
        }

        /// <summary>
        /// 读取 scores.db（用于未来合并进现有文件；当前导出路径可不调用）。
        /// </summary>
        public static IReadOnlyList<LegacyScoresDbBeatmapGroup> ReadFile(string path)
        {
            using var stream = File.OpenRead(path);
            return Read(stream);
        }

        public static IReadOnlyList<LegacyScoresDbBeatmapGroup> Read(Stream stream)
        {
            if (stream.Length == 0)
                return Array.Empty<LegacyScoresDbBeatmapGroup>();

            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            reader.ReadInt32(); // version
            int beatmapCount = reader.ReadInt32();
            if (beatmapCount < 0)
                throw new InvalidDataException("scores.db 谱面组数量无效。");

            var result = new List<LegacyScoresDbBeatmapGroup>(beatmapCount);

            for (int i = 0; i < beatmapCount; i++)
            {
                string md5 = readOsuString(reader) ?? string.Empty;
                int scoreCount = reader.ReadInt32();
                if (scoreCount < 0)
                    throw new InvalidDataException($"scores.db 谱面「{md5}」成绩数量无效。");

                var scores = new List<LegacyScoresDbScore>(scoreCount);
                for (int j = 0; j < scoreCount; j++)
                    scores.Add(readScore(reader));

                result.Add(new LegacyScoresDbBeatmapGroup(md5, scores));
            }

            return result;
        }

        private static void writeScore(BinaryWriter writer, LegacyScoresDbScore score)
        {
            writer.Write(score.GameplayMode);
            writer.Write(score.Version);
            writeOsuString(writer, score.BeatmapMd5);
            writeOsuString(writer, score.PlayerName);
            writeOsuString(writer, score.ReplayMd5);
            writer.Write(score.Count300);
            writer.Write(score.Count100);
            writer.Write(score.Count50);
            writer.Write(score.CountGeki);
            writer.Write(score.CountKatu);
            writer.Write(score.CountMiss);
            writer.Write(score.TotalScore);
            writer.Write(score.MaxCombo);
            writer.Write(score.PerfectCombo);
            writer.Write(score.Mods);
            writeOsuString(writer, string.Empty);
            writer.Write(score.TimestampTicks);
            writer.Write(unchecked((int)0xffffffff));
            writer.Write(score.OnlineScoreId);

            if (score.AdditionalModInfo is double extra)
                writer.Write(extra);
        }

        private static LegacyScoresDbScore readScore(BinaryReader reader)
        {
            byte mode = reader.ReadByte();
            int version = reader.ReadInt32();
            string beatmapMd5 = readOsuString(reader) ?? string.Empty;
            string player = readOsuString(reader) ?? string.Empty;
            string replayMd5 = readOsuString(reader) ?? string.Empty;
            ushort c300 = reader.ReadUInt16();
            ushort c100 = reader.ReadUInt16();
            ushort c50 = reader.ReadUInt16();
            ushort geki = reader.ReadUInt16();
            ushort katu = reader.ReadUInt16();
            ushort miss = reader.ReadUInt16();
            int total = reader.ReadInt32();
            ushort maxCombo = reader.ReadUInt16();
            bool perfect = reader.ReadBoolean();
            int mods = reader.ReadInt32();
            readOsuString(reader); // empty
            long ticks = reader.ReadInt64();
            reader.ReadInt32(); // -1
            long onlineId = reader.ReadInt64();

            // Target Practice 额外 double：仅当还有剩余数据且能完整读出时才消费（合并写回时由写出端决定是否带）。
            // 读取整文件时若下一组长度错位会失败；此处保守：不尝试读 optional double。
            return new LegacyScoresDbScore
            {
                GameplayMode = mode,
                Version = version,
                BeatmapMd5 = beatmapMd5,
                PlayerName = player,
                ReplayMd5 = replayMd5,
                Count300 = c300,
                Count100 = c100,
                Count50 = c50,
                CountGeki = geki,
                CountKatu = katu,
                CountMiss = miss,
                TotalScore = total,
                MaxCombo = maxCombo,
                PerfectCombo = perfect,
                Mods = mods,
                TimestampTicks = ticks,
                OnlineScoreId = onlineId,
            };
        }

        private static string? readOsuString(BinaryReader reader)
        {
            byte flag = reader.ReadByte();
            if (flag == 0)
                return null;

            if (flag != string_flag)
                throw new InvalidDataException($"scores.db 字符串标记无效：0x{flag:X2}。");

            return reader.ReadString();
        }

        private static void writeOsuString(BinaryWriter writer, string? value)
        {
            if (value == null)
            {
                writer.Write((byte)0);
                return;
            }

            writer.Write(string_flag);
            writer.Write(value);
        }
    }

    public sealed class LegacyScoresDbBeatmapGroup
    {
        public LegacyScoresDbBeatmapGroup(string? beatmapMd5, IReadOnlyList<LegacyScoresDbScore>? scores)
        {
            BeatmapMd5 = beatmapMd5 ?? string.Empty;
            Scores = scores ?? Array.Empty<LegacyScoresDbScore>();
        }

        public string BeatmapMd5 { get; }

        public IReadOnlyList<LegacyScoresDbScore> Scores { get; }
    }

    public sealed class LegacyScoresDbScore
    {
        public byte GameplayMode { get; init; }

        public int Version { get; init; }

        public string BeatmapMd5 { get; init; } = string.Empty;

        public string PlayerName { get; init; } = string.Empty;

        public string ReplayMd5 { get; init; } = string.Empty;

        public ushort Count300 { get; init; }

        public ushort Count100 { get; init; }

        public ushort Count50 { get; init; }

        public ushort CountGeki { get; init; }

        public ushort CountKatu { get; init; }

        public ushort CountMiss { get; init; }

        public int TotalScore { get; init; }

        public ushort MaxCombo { get; init; }

        public bool PerfectCombo { get; init; }

        public int Mods { get; init; }

        public long TimestampTicks { get; init; }

        public long OnlineScoreId { get; init; } = -1;

        public double? AdditionalModInfo { get; init; }
    }
}
