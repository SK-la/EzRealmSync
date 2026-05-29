using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.IO
{
    /// <summary>
    /// 扫描备份目录中的 <c>*.realm</c> 文件，并支持将备份还原到目标库路径。
    /// </summary>
    public static class RealmBackupCatalog
    {
        private static readonly Regex timestampedBackupPattern = new(
            @"^(.+)_\d{8}_\d{6}\.realm$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static string CreateEntryId(string backupFilePath) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(backupFilePath))));

        public static IReadOnlyList<BackupEntry> List(string backupDirectory)
        {
            if (string.IsNullOrWhiteSpace(backupDirectory) || !Directory.Exists(backupDirectory))
                return Array.Empty<BackupEntry>();

            return Directory.EnumerateFiles(backupDirectory, "*.realm", SearchOption.TopDirectoryOnly)
                .Select(path => toEntry(path))
                .OrderByDescending(e => e.CreatedAt)
                .ToList();
        }

        public static bool TryFind(string backupDirectory, string backupId, out BackupEntry entry)
        {
            entry = null!;

            foreach (var candidate in List(backupDirectory))
            {
                if (string.Equals(candidate.Id, backupId, StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }

        public static bool TryInferOriginalFileName(string backupFileName, out string originalFileName)
        {
            var match = timestampedBackupPattern.Match(backupFileName);

            if (!match.Success)
            {
                originalFileName = backupFileName;
                return false;
            }

            originalFileName = match.Groups[1].Value + ".realm";
            return true;
        }

        public static BackupEntry ToEntry(string backupFilePath)
        {
            string fullPath = Path.GetFullPath(backupFilePath);
            var info = new FileInfo(fullPath);
            string fileName = info.Name;

            TryInferOriginalFileName(fileName, out string originalName);

            return new BackupEntry
            {
                Id = CreateEntryId(fullPath),
                CreatedAt = info.Exists ? info.CreationTimeUtc : DateTimeOffset.UtcNow,
                Description = originalName != fileName ? $"还原为 {originalName}" : fileName,
                Path = fullPath,
            };
        }

        private static BackupEntry toEntry(string path) => ToEntry(path);
    }
}
