using System.Text;

namespace osu.Game.EzRealmSync.IO
{
    /// <summary>
    /// osu!stable <c>collection.db</c> 编解码（社区也常写作 <c>collections.db</c>）。
    /// 格式：version int32 + collectionCount int32 + [name string + mapCount int32 + [md5 string]…]。
    /// 字符串为 osu 专用：先写 0x00（null）或 0x0b（有值）再跟 .NET BinaryWriter UTF-8 字符串。
    /// </summary>
    public static class LegacyCollectionDb
    {
        public const string StableFileName = "collection.db";
        public const string AlternateFileName = "collections.db";

        /// <summary>写出时使用的版本号；osu!stable 会忽略未知版本并照常读取条目。</summary>
        public const int DefaultVersion = 20250108;

        private const byte string_flag = 0x0b;

        public static bool IsCollectionDbFileName(string? pathOrName)
        {
            if (string.IsNullOrWhiteSpace(pathOrName))
                return false;

            string name = Path.GetFileName(pathOrName.Trim());
            return name.Equals(StableFileName, StringComparison.OrdinalIgnoreCase)
                   || name.Equals(AlternateFileName, StringComparison.OrdinalIgnoreCase);
        }

        public static string ResolveOutputFile(string outputDirectory, string? folderOrFileName)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new ArgumentException("输出目录不能为空。", nameof(outputDirectory));

            string trimmed = folderOrFileName?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(trimmed))
            {
                string folder = $"collections-{DateTime.Now:yyyyMMdd_HHmmss}";
                return Path.Combine(outputDirectory, folder, StableFileName);
            }

            if (IsCollectionDbFileName(trimmed) || trimmed.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                return Path.Combine(outputDirectory, Path.GetFileName(trimmed));

            return Path.Combine(outputDirectory, trimmed, StableFileName);
        }

        public static IReadOnlyList<LegacyCollectionDbEntry> ReadFile(string path)
        {
            using var stream = File.OpenRead(path);
            return Read(stream);
        }

        public static IReadOnlyList<LegacyCollectionDbEntry> Read(Stream stream)
        {
            if (stream.Length == 0)
                return Array.Empty<LegacyCollectionDbEntry>();

            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            reader.ReadInt32(); // version（stable / Collection Manager 均忽略）

            int collectionCount = reader.ReadInt32();
            if (collectionCount < 0)
                throw new InvalidDataException("collection.db 收藏夹数量无效。");

            var result = new List<LegacyCollectionDbEntry>(collectionCount);

            for (int i = 0; i < collectionCount; i++)
            {
                string name = readOsuString(reader) ?? string.Empty;
                int mapCount = reader.ReadInt32();
                if (mapCount < 0)
                    throw new InvalidDataException($"收藏夹「{name}」的谱面数量无效。");

                var hashes = new List<string>(mapCount);

                for (int j = 0; j < mapCount; j++)
                {
                    string? hash = readOsuString(reader);
                    if (!string.IsNullOrEmpty(hash))
                        hashes.Add(hash);
                }

                result.Add(new LegacyCollectionDbEntry(name, hashes));
            }

            return result;
        }

        public static void WriteFile(string path, IEnumerable<LegacyCollectionDbEntry> collections, int version = DefaultVersion)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using var stream = File.Create(path);
            Write(stream, collections, version);
        }

        public static void Write(Stream stream, IEnumerable<LegacyCollectionDbEntry> collections, int version = DefaultVersion)
        {
            var list = collections as IList<LegacyCollectionDbEntry> ?? collections.ToList();

            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(version);
            writer.Write(list.Count);

            foreach (var collection in list)
            {
                writeOsuString(writer, collection.Name);
                writer.Write(collection.BeatmapMd5Hashes.Count);

                foreach (string hash in collection.BeatmapMd5Hashes)
                    writeOsuString(writer, hash);
            }

            writer.Flush();
        }

        private static string? readOsuString(BinaryReader reader)
        {
            byte flag = reader.ReadByte();
            if (flag == 0)
                return null;

            if (flag != string_flag)
                throw new InvalidDataException($"collection.db 字符串标记无效：0x{flag:X2}。");

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

    public sealed class LegacyCollectionDbEntry
    {
        public LegacyCollectionDbEntry(string name, IReadOnlyList<string> beatmapMd5Hashes)
        {
            Name = name ?? string.Empty;
            BeatmapMd5Hashes = beatmapMd5Hashes ?? Array.Empty<string>();
        }

        public string Name { get; }

        public IReadOnlyList<string> BeatmapMd5Hashes { get; }
    }
}
