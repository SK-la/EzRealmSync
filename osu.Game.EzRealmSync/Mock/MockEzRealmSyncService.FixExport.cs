using osu.Game.EzRealmSync.Abstractions;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Mock
{
    public sealed partial class MockEzRealmSyncService : IRealmFixService, IRealmExportService
    {
        private readonly Dictionary<string, List<RealmFixIssue>> fixIssuesByRealm = new();
        private readonly Dictionary<(string realmId, ExportDataKind kind), RealmExportCatalog> exportCatalogs = new();

        public async Task<IReadOnlyList<RealmFixIssue>> ScanIssuesAsync(
            string realmId,
            string workspacePath,
            RealmFixScanOptions options,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var snapshot = await ensureLoadedAsync(realmId, progress, cancellationToken).ConfigureAwait(false);
            await simulateWorkAsync(progress, "正在扫描修复项…", cancellationToken).ConfigureAwait(false);

            var issues = new List<RealmFixIssue>();
            string filesDir = RealmWorkspacePaths.TryResolveFilesDirectory(workspacePath, out string resolved)
                ? resolved
                : Path.Combine(workspacePath, "files");

            foreach (var group in snapshot.Groups)
            {
                foreach (var row in group.Rows)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (options.ScanIllegalCharacters)
                    {
                        foreach (char illegal in options.IllegalCharacters)
                        {
                            if (!row.Title.Contains(illegal))
                                continue;

                            issues.Add(new RealmFixIssue
                            {
                                Id = Guid.NewGuid(),
                                Kind = RealmFixIssueKind.IllegalCharacter,
                                EntityKind = group.EntityKind,
                                TargetEntityId = row.Id,
                                FieldName = nameof(RealmEntityRow.Title),
                                CurrentValue = row.Title,
                                SuggestedValue = row.Title.Replace(illegal, options.IllegalCharacterReplacement.Length > 0 ? options.IllegalCharacterReplacement[0] : '_'),
                                Detail = $"包含非法字符 '{illegal}'",
                            });
                            break;
                        }
                    }

                    if (options.ScanMissingFiles && group.EntityKind != EntityKind.Score)
                    {
                        string relative = buildMockRelativePath(row, group.EntityKind);
                        string expected = Path.Combine(filesDir, relative);

                        if (!File.Exists(expected) && !Directory.Exists(expected))
                        {
                            issues.Add(new RealmFixIssue
                            {
                                Id = Guid.NewGuid(),
                                Kind = RealmFixIssueKind.MissingFile,
                                EntityKind = group.EntityKind,
                                FieldName = "Files",
                                CurrentValue = relative,
                                SuggestedValue = string.Empty,
                                Detail = "Realm 有条目但 files 中缺少对应文件",
                                ExpectedFilePath = expected,
                            });
                        }
                    }
                }
            }

            fixIssuesByRealm[realmId] = issues;
            return issues;
        }

        public async Task<RealmFixApplyResult> ApplyFixesAsync(
            string realmId,
            string workspacePath,
            IReadOnlyList<Guid> issueIds,
            RealmFixApplyOptions options,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!fixIssuesByRealm.TryGetValue(realmId, out var issues))
                return new RealmFixApplyResult();

            await simulateWorkAsync(progress, "正在应用修复…", cancellationToken).ConfigureAwait(false);

            var idSet = issueIds.ToHashSet();
            int applied = 0;
            int skipped = 0;

            foreach (var issue in issues)
            {
                if (!idSet.Contains(issue.Id))
                    continue;

                if (issue.Kind == RealmFixIssueKind.MissingFile)
                {
                    if (issue.ExpectedFilePath != null)
                    {
                        string? dir = Path.GetDirectoryName(issue.ExpectedFilePath);
                        if (!string.IsNullOrEmpty(dir))
                            Directory.CreateDirectory(dir);

                        await File.WriteAllTextAsync(issue.ExpectedFilePath, $"// mock restored file for {issue.CurrentValue}", cancellationToken).ConfigureAwait(false);
                    }

                    applied++;
                }
                else if (issue.Kind == RealmFixIssueKind.IllegalCharacter && issue.TargetEntityId != null)
                {
                    if (applyIllegalCharacterToSnapshot(realmId, issue))
                        applied++;
                    else
                        skipped++;
                }
                else if (issue.Kind == RealmFixIssueKind.OrphanFile && issue.ExpectedFilePath != null)
                {
                    if (File.Exists(issue.ExpectedFilePath))
                    {
                        File.Delete(issue.ExpectedFilePath);
                        applied++;
                    }
                }
                else
                {
                    skipped++;
                }
            }

            fixIssuesByRealm[realmId] = issues.Where(i => !idSet.Contains(i.Id)).ToList();
            return new RealmFixApplyResult { AppliedCount = applied, SkippedCount = skipped };
        }

        public async Task<RealmExportCatalog> LoadCatalogAsync(
            string realmId,
            ExportDataKind kind,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var snapshot = await ensureLoadedAsync(realmId, progress, cancellationToken).ConfigureAwait(false);
            await simulateWorkAsync(progress, "正在加载导出列表…", cancellationToken).ConfigureAwait(false);

            var key = (realmId, kind);
            if (exportCatalogs.TryGetValue(key, out var cached))
                return cached;

            var items = new List<RealmExportItem>();

            switch (kind)
            {
                case ExportDataKind.BeatmapSet:
                    foreach (var row in snapshot.Groups.First(g => g.EntityKind == EntityKind.BeatmapSet).Rows)
                    {
                        items.Add(new RealmExportItem
                        {
                            Id = row.Id,
                            Title = row.Title,
                            Artist = row.Artist,
                            RelativePath = $"{row.Artist} - {row.Title}",
                        });
                    }

                    break;

                case ExportDataKind.Beatmap:
                    foreach (var row in snapshot.Groups.First(g => g.EntityKind == EntityKind.Beatmap).Rows)
                    {
                        items.Add(new RealmExportItem
                        {
                            Id = row.Id,
                            Title = row.Title,
                            Artist = row.Artist,
                            RelativePath = $"{row.Artist} - {row.Title}/{row.Ruleset}.osu",
                        });
                    }

                    break;

                case ExportDataKind.Collection:
                    foreach (var collectionRow in snapshot.Groups.First(g => g.EntityKind == EntityKind.BeatmapCollection).Rows)
                    {
                        string collection = collectionRow.Title;

                        foreach (var row in snapshot.Groups.First(g => g.EntityKind == EntityKind.Beatmap).Rows)
                        {
                            items.Add(new RealmExportItem
                            {
                                Id = Guid.NewGuid(),
                                Title = row.Title,
                                Artist = row.Artist,
                                CollectionName = collection,
                                RelativePath = $"{row.Artist} - {row.Title}/{row.Ruleset}.osu",
                            });
                        }
                    }

                    break;

                case ExportDataKind.Score:
                    foreach (var row in snapshot.Groups.First(g => g.EntityKind == EntityKind.Score).Rows)
                    {
                        string destName = $"{row.Title} ({row.Date?.LocalDateTime ?? DateTime.Now:yyyy-MM-dd_HH-mm}).osr";
                        items.Add(new RealmExportItem
                        {
                            Id = row.Id,
                            Title = row.Title,
                            Artist = row.Artist,
                            RelativePath = $"0/0/{row.Hash}",
                            DestinationRelativePath = Path.Combine("replays", destName),
                        });
                    }

                    break;
            }

            var catalog = new RealmExportCatalog { Kind = kind, Items = items };
            exportCatalogs[key] = catalog;
            return catalog;
        }

        public async Task<RealmExportResult> ExportAsync(
            RealmExportRequest request,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!exportCatalogs.TryGetValue((request.RealmId, request.Kind), out var catalog))
                catalog = await LoadCatalogAsync(request.RealmId, request.Kind, progress, cancellationToken).ConfigureAwait(false);

            string folderName = string.IsNullOrWhiteSpace(request.FolderName)
                ? request.Kind == ExportDataKind.Score
                    ? $"replays-{DateTime.Now:yyyyMMdd_HHmmss}"
                    : $"songs-{DateTime.Now:yyyyMMdd_HHmmss}"
                : request.FolderName.Trim();

            string outputRoot = Path.Combine(request.OutputDirectory, folderName);
            Directory.CreateDirectory(outputRoot);

            var idSet = request.ItemIds.ToHashSet();
            int exported = 0;
            int skipped = 0;
            int index = 0;

            foreach (var item in catalog.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!idSet.Contains(item.Id))
                    continue;

                index++;
                progress?.Report(new ScanProgress
                {
                    Progress = (double)index / Math.Max(1, idSet.Count),
                    Message = item.Title,
                });

                string targetDir = request.Kind == ExportDataKind.Collection && !string.IsNullOrEmpty(item.CollectionName)
                    ? Path.Combine(outputRoot, sanitizePathSegment(item.CollectionName))
                    : outputRoot;

                string destRelative = string.IsNullOrWhiteSpace(item.DestinationRelativePath)
                    ? item.RelativePath
                    : item.DestinationRelativePath;

                string destPath = Path.Combine(targetDir, destRelative);
                string? destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);

                string sourcePath = Path.Combine(request.FilesDirectory, item.RelativePath);
                if (File.Exists(sourcePath))
                    File.Copy(sourcePath, destPath, overwrite: true);
                else
                    await File.WriteAllTextAsync(destPath, $"// mock export\n// {item.Artist} - {item.Title}", cancellationToken).ConfigureAwait(false);

                exported++;
            }

            skipped = idSet.Count - exported;
            await simulateWorkAsync(progress, "导出完成", cancellationToken).ConfigureAwait(false);

            return new RealmExportResult
            {
                OutputRoot = outputRoot,
                ExportedCount = exported,
                SkippedCount = skipped,
            };
        }

        public Task<RealmExportResult> ExportBrowseEntitiesAsync(
            string realmId,
            string filesDirectory,
            RealmObjectClass objectClass,
            IReadOnlyList<Guid> entityIds,
            string outputDirectory,
            string? folderName = null,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            ExportAsync(
                new RealmExportRequest
                {
                    RealmId = realmId,
                    Kind = objectClass switch
                    {
                        RealmObjectClass.BeatmapCollection => ExportDataKind.Collection,
                        RealmObjectClass.Score => ExportDataKind.Score,
                        _ => ExportDataKind.BeatmapSet,
                    },
                    FilesDirectory = filesDirectory,
                    OutputDirectory = outputDirectory,
                    FolderName = folderName,
                    ItemIds = entityIds,
                },
                progress,
                cancellationToken);

        private static string buildMockRelativePath(RealmEntityRow row, EntityKind kind) => kind == EntityKind.BeatmapSet
            ? $"{row.Artist} - {row.Title}"
            : $"{row.Artist} - {row.Title}/{row.Ruleset}.osu";

        private static string sanitizePathSegment(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            return name.Trim();
        }

        private bool applyIllegalCharacterToSnapshot(string realmId, RealmFixIssue issue)
        {
            if (!loadedSnapshots.TryGetValue(realmId, out var snapshot) || issue.TargetEntityId is not Guid targetId)
                return false;

            bool updated = false;
            var newGroups = snapshot.Groups.Select(group =>
            {
                var newRows = group.Rows.Select(row =>
                {
                    if (row.Id != targetId)
                        return row;

                    updated = true;
                    return new RealmEntityRow
                    {
                        Id = row.Id,
                        EntityKind = row.EntityKind,
                        Title = issue.SuggestedValue,
                        Artist = row.Artist,
                        Hash = row.Hash,
                        Ruleset = row.Ruleset,
                        Date = row.Date,
                        Extra = row.Extra,
                    };
                }).ToList();

                return new RealmGroupSnapshot { EntityKind = group.EntityKind, Rows = newRows };
            }).ToList();

            if (!updated)
                return false;

            loadedSnapshots[realmId] = new RealmSnapshot
            {
                RealmId = snapshot.RealmId,
                DisplayName = snapshot.DisplayName,
                Classes = snapshot.Classes,
                Groups = newGroups,
            };

            return true;
        }
    }
}
