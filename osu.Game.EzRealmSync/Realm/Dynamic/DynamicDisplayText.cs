namespace osu.Game.EzRealmSync.Realm.Dynamic
{
    /// <summary>
    /// 官方展示文本的**动态复刻**：不加载 osu.Game 模型，只按 schema 读列拼字符串。
    ///
    /// 这几条规则是产品文案的一部分（导出文件名、导出列表标题都靠它），必须与官方逐字一致，
    /// 否则同一份库在两个浏览入口里显示不同。规则来源：官方 <c>ModelExtensions.GetDisplayString</c> 一族。
    ///
    /// 缺字段时的兜底也照抄官方："unknown artist" / "unknown title" / "unknown"，不要改成空串。
    /// </summary>
    internal static class DynamicDisplayText
    {
        /// <summary>谱面元数据：<c>Artist - Title (Creator)</c>。</summary>
        public static string MetadataTitle(string? artist, string? title, string? author) =>
            $"{orDefault(artist, "unknown artist")} - {orDefault(title, "unknown title")}{authorSuffix(author)}".Trim();

        /// <summary>难度：元数据标题后接 <c>[难度名]</c>。</summary>
        public static string BeatmapTitle(string? artist, string? title, string? author, string? difficultyName) =>
            $"{MetadataTitle(artist, title, author)} {versionSuffix(difficultyName)}".Trim();

        /// <summary>成绩：<c>玩家 playing 难度标题</c>；没链到难度就是 <c>unknown</c>。</summary>
        public static string ScoreTitle(string? username, string? beatmapTitle) =>
            $"{username} playing {orDefault(beatmapTitle, "unknown")}";

        private static string authorSuffix(string? author) =>
            string.IsNullOrEmpty(author) ? string.Empty : $" ({author})";

        private static string versionSuffix(string? difficultyName) =>
            string.IsNullOrEmpty(difficultyName) ? string.Empty : $"[{difficultyName}]";

        private static string orDefault(string? value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;

        /// <summary>
        /// 官方 <c>ModelExtensions.GetValidFilename</c> 的字符集，**刻意不跟随平台**
        /// （<see cref="Path.GetInvalidFileNameChars"/> 各系统不同，那会让导出名跨平台不一致）。
        /// 官方注释标明这份集合与 stable 对齐，属互操作契约，别"顺手改成本地平台更全的集合"。
        /// </summary>
        private static readonly char[] invalid_filename_chars =
        [
            '"', '<', '>', '|', '\0', (char)1, (char)2, (char)3, (char)4, (char)5, (char)6, (char)7, (char)8, (char)9, (char)10, (char)11, (char)12, (char)13, (char)14, (char)15, (char)16, (char)17,
            (char)18, (char)19, (char)20, (char)21, (char)22, (char)23, (char)24, (char)25, (char)26, (char)27, (char)28, (char)29, (char)30, (char)31, ':', '*', '?', '\\', '/',
        ];

        public static string ValidFilename(string filename)
        {
            foreach (char c in invalid_filename_chars)
                filename = filename.Replace(c.ToString(), string.Empty);

            return filename;
        }

        /// <summary>
        /// 导出目录名的兜底：官方在路径里会把 <c>/</c> 也换掉（<see cref="SanitizePathSegment"/>），
        /// 因为收藏夹名允许带斜杠，直接拼路径会跑出目标目录。
        /// </summary>
        public static string SanitizePathSegment(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            return name.Trim();
        }
    }
}
