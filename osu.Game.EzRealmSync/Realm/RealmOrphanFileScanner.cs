using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 扫描 <c>files/</c> 与 Realm <c>RealmFile</c> 表的一致性（缺失 / 僵尸文件）。
    /// </summary>
    public static class RealmOrphanFileScanner
    {
#if HAS_EZ_OSU_GAME
        public static void ScanMissingReferencedFiles(
            osu.Game.Database.RealmAccess access,
            string filesDirectory,
            List<RealmFixIssue> issues,
            CancellationToken cancellationToken)
        {
            access.Run(realm =>
            {
                foreach (var file in realm.All<osu.Game.Models.RealmFile>())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string expected = RealmFilePathHelper.GetFullPath(filesDirectory, file.Hash);

                    if (!File.Exists(expected))
                    {
                        issues.Add(new RealmFixIssue
                        {
                            Id = Guid.NewGuid(),
                            Kind = RealmFixIssueKind.MissingFile,
                            EntityKind = EntityKind.BeatmapSet,
                            FieldName = "File",
                            CurrentValue = file.Hash,
                            SuggestedValue = string.Empty,
                            Detail = "Realm 文件表有条目但 files/ 中缺少实体文件",
                            ExpectedFilePath = expected,
                        });
                    }
                }
            });
        }

        public static void ScanOrphansOnDisk(
            osu.Game.Database.RealmAccess access,
            string filesDirectory,
            List<RealmFixIssue> issues,
            CancellationToken cancellationToken,
            int maxIssues = 500)
        {
            var referencedHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            access.Run(realm =>
            {
                foreach (var file in realm.All<osu.Game.Models.RealmFile>())
                    referencedHashes.Add(file.Hash);
            });

            if (!Directory.Exists(filesDirectory))
                return;

            foreach (string path in Directory.EnumerateFiles(filesDirectory, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (issues.Count >= maxIssues)
                    break;

                string? hash = tryExtractHashFromStoragePath(filesDirectory, path);
                if (hash == null || referencedHashes.Contains(hash))
                    continue;

                issues.Add(new RealmFixIssue
                {
                    Id = Guid.NewGuid(),
                    Kind = RealmFixIssueKind.OrphanFile,
                    EntityKind = EntityKind.BeatmapSet,
                    FieldName = "File",
                    CurrentValue = hash,
                    SuggestedValue = string.Empty,
                    Detail = "磁盘存在但 Realm 未引用的文件",
                    ExpectedFilePath = path,
                });
            }
        }

        private static string? tryExtractHashFromStoragePath(string filesDirectory, string fullPath)
        {
            string relative = Path.GetRelativePath(Path.GetFullPath(filesDirectory), Path.GetFullPath(fullPath));
            string fileName = Path.GetFileName(relative);

            if (fileName.Length == 32 && fileName.All(Uri.IsHexDigit))
                return fileName;

            return null;
        }
#endif

        public static int DeleteOrphanFiles(IReadOnlyList<RealmFixIssue> issues)
        {
            int deleted = 0;

            foreach (var issue in issues)
            {
                if (issue.Kind != RealmFixIssueKind.OrphanFile || string.IsNullOrEmpty(issue.ExpectedFilePath))
                    continue;

                try
                {
                    if (File.Exists(issue.ExpectedFilePath))
                    {
                        File.Delete(issue.ExpectedFilePath);
                        deleted++;
                    }
                }
                catch
                {
                }
            }

            return deleted;
        }
    }
}
