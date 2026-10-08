using System.Text;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 打开和浏览 Realm 时写入可排查的现场：文件大小、头字节、schema、行数，以及疑似乱码的文本样本。
    /// </summary>
    internal static class RealmReadLog
    {
        private const int max_inspected_cells = 2500;
        private const int max_samples = 8;
        private const int sample_chars = 48;

        public static void Opened(DynamicRealmSession session)
        {
            var builder = new StringBuilder();
            builder.Append("Realm 已打开 ");
            builder.Append(DescribeFile(session.FilePath));
            builder.Append($" readOnly={session.IsReadOnly} schema={session.DiskSchemaVersion}");

            try
            {
                var names = session.Realm.Schema.Select(schema => schema.Name).ToList();
                builder.Append($" classCount={names.Count}");
                if (names.Count > 0)
                    builder.Append(" classes=").Append(string.Join(",", names.Take(40)));
            }
            catch (Exception ex)
            {
                builder.Append($" schemaListFailed={ex.GetType().Name}:{ex.Message}");
            }

            EzRealmSyncLog.Info(builder.ToString());
        }

        public static void OpenFailed(string path, Exception ex) =>
            EzRealmSyncLog.Exception(ex, "打开 Realm 失败 " + DescribeFile(path));

        public static void BrowseSnapshot(RealmSnapshot snapshot)
        {
            string counts = snapshot.Classes.Count == 0
                ? "(无类型)"
                : string.Join(", ", snapshot.Classes.Select(group => $"{group.Class}={group.Count}"));

            EzRealmSyncLog.Info($"浏览快照 name={snapshot.DisplayName} rows={snapshot.TotalRowCount} {counts}");
            logSuspiciousText(snapshot);
        }

        public static string DescribeFile(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                    return $"path={path} missing=true";

                return $"path={info.FullName} bytes={info.Length} mtime={info.LastWriteTimeUtc:yyyy-MM-dd HH:mm:ss}Z header={readHeader(info.FullName)}";
            }
            catch (Exception ex)
            {
                return $"path={path} statFailed={ex.GetType().Name}:{ex.Message}";
            }
        }

        internal static string? DescribeAnomaly(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            foreach (char character in text)
            {
                if (character == '\uFFFD')
                    return "U+FFFD";

                if (char.IsSurrogate(character))
                    return "surrogate";

                if (character is '\uFFFE' or '\uFFFF')
                    return "noncharacter";

                if (char.IsControl(character) && character is not '\t' and not '\n' and not '\r')
                    return "control";
            }

            return null;
        }

        internal static string EscapeSample(string text)
        {
            var builder = new StringBuilder();
            int taken = 0;

            foreach (char character in text)
            {
                if (taken >= sample_chars)
                {
                    builder.Append('…');
                    break;
                }

                if (character is >= ' ' and <= '~')
                    builder.Append(character);
                else
                    builder.Append($"\\u{(int)character:X4}");

                taken++;
            }

            return builder.ToString();
        }

        private static void logSuspiciousText(RealmSnapshot snapshot)
        {
            int inspected = 0;
            int suspicious = 0;
            var samples = new List<string>();

            foreach (RealmClassGroup group in snapshot.Classes)
            {
                foreach (RealmBrowseRow row in group.Rows)
                {
                    foreach (var cell in row.Cells)
                    {
                        if (inspected >= max_inspected_cells)
                            break;

                        inspected++;
                        string? reason = DescribeAnomaly(cell.Value);
                        if (reason == null)
                            continue;

                        suspicious++;
                        if (samples.Count < max_samples)
                            samples.Add($"{group.Class}.{cell.Key} {reason} sample={EscapeSample(cell.Value)}");
                    }

                    if (inspected >= max_inspected_cells)
                        break;
                }

                if (inspected >= max_inspected_cells)
                    break;
            }

            if (suspicious == 0)
            {
                EzRealmSyncLog.Info($"文本抽查 cells={inspected} 未发现替换符、孤立代理项或控制字符。");
                return;
            }

            EzRealmSyncLog.Warn($"文本抽查 cells={inspected} suspicious={suspicious}（最多记录 {max_samples} 条，扫描上限 {max_inspected_cells} 格）");
            foreach (string sample in samples)
                EzRealmSyncLog.Warn("  " + sample);
        }

        private static string readHeader(string path)
        {
            var buffer = new byte[16];
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            int read = stream.Read(buffer, 0, buffer.Length);
            return Convert.ToHexString(buffer.AsSpan(0, read));
        }
    }
}
