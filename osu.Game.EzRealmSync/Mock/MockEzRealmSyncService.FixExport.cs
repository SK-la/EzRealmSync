using osu.Game.EzRealmSync.Abstractions;
using osu.Game.EzRealmSync.IO;
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

        public async Task<RealmOfficialConversionResult> ConvertToOfficialRealmAsync(
            string realmId,
            string? outputRealmFilePath = null,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var snapshot = await ensureLoadedAsync(realmId, progress, cancellationToken).ConfigureAwait(false);
            await simulateWorkAsync(progress, "正在转换为官方库…", cancellationToken).ConfigureAwait(false);

            string target = string.IsNullOrWhiteSpace(outputRealmFilePath)
                ? Path.Combine(Path.GetTempPath(), $"client_{Guid.NewGuid():N}.realm")
                : Path.GetFullPath(outputRealmFilePath.Trim());

            string? dir = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(target, $"// mock official realm converted from {snapshot.DisplayName}", cancellationToken).ConfigureAwait(false);

            return new RealmOfficialConversionResult
            {
                TargetRealmFilePath = target,
                AppliedCount = snapshot.Groups.Sum(g => g.Rows.Count),
                BackupPath = Path.Combine(Path.GetTempPath(), $"client_backup_{Guid.NewGuid():N}.realm"),
            };
        }

        public async Task<RealmSchemaUpgradeResult> UpgradeSchemaToLatestAsync(
            string realmId,
            string? backupDirectory = null,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            await ensureLoadedAsync(realmId, progress, cancellationToken).ConfigureAwait(false);
            await simulateWorkAsync(progress, "正在升级 schema…", cancellationToken).ConfigureAwait(false);

            return new RealmSchemaUpgradeResult
            {
                RealmFilePath = Path.Combine(Path.GetTempPath(), "client_mock.realm"),
                SourceSchemaVersion = 51_003,
                TargetSchemaVersion = 51_006,
                BackupPath = Path.Combine(backupDirectory ?? EzRealmSyncDefaults.DefaultBackupDirectory, "mock_backup.realm"),
            };
        }

        public void InvalidateCatalog(string? realmId = null)
        {
            if (realmId == null)
            {
                exportCatalogs.Clear();
                return;
            }

            foreach (var key in exportCatalogs.Keys.Where(k => k.realmId == realmId).ToList())
                exportCatalogs.Remove(key);
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
                case ExportDataKind.CollectionDb:
                    foreach (var collectionRow in snapshot.Groups.First(g => g.EntityKind == EntityKind.BeatmapCollection).Rows)
                    {
                        int count = snapshot.Groups.First(g => g.EntityKind == EntityKind.Beatmap).Rows.Count;
                        items.Add(new RealmExportItem
                        {
                            Id = collectionRow.Id,
                            Title = collectionRow.Title,
                            CollectionName = collectionRow.Title,
                            BeatmapCount = count,
                        });
                    }

                    break;

                case ExportDataKind.Score:
                    foreach (var row in snapshot.Groups.First(g => g.EntityKind == EntityKind.Score).Rows)
                    {
                        string player = row.Artist;
                        string destName = $"{row.Title} ({row.Date?.LocalDateTime ?? DateTime.Now:yyyy-MM-dd_HH-mm}).osr";
                        string dest = !string.IsNullOrWhiteSpace(player)
                            ? Path.Combine("replays", sanitizePathSegment(player), destName)
                            : Path.Combine("replays", destName);

                        items.Add(new RealmExportItem
                        {
                            Id = row.Id,
                            Title = row.Title,
                            Artist = row.Artist,
                            PlayerName = player,
                            RelativePath = $"0/0/{row.Hash}",
                            DestinationRelativePath = dest,
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

            var idSet = request.ItemIds.ToHashSet();

            if (request.Kind == ExportDataKind.CollectionDb)
            {
                var snapshot = await ensureLoadedAsync(request.RealmId, progress, cancellationToken).ConfigureAwait(false);
                var entries = new List<LegacyCollectionDbEntry>();

                foreach (var collectionRow in catalog.Items.Where(i => idSet.Contains(i.Id)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int count = collectionRow.BeatmapCount > 0
                        ? collectionRow.BeatmapCount
                        : snapshot.Groups.FirstOrDefault(g => g.EntityKind == EntityKind.Beatmap)?.Rows.Count ?? 0;
                    var hashes = GetOrCreateMockCollectionHashes(request.RealmId, collectionRow.Id, count);
                    entries.Add(new LegacyCollectionDbEntry(collectionRow.Title, hashes));
                }

                string outputFile = LegacyCollectionDb.ResolveOutputFile(request.OutputDirectory, request.FolderName);
                LegacyCollectionDb.WriteFile(outputFile, entries);
                await simulateWorkAsync(progress, "导出完成", cancellationToken).ConfigureAwait(false);

                return new RealmExportResult
                {
                    OutputRoot = outputFile,
                    ExportedCount = entries.Count,
                    SkippedCount = 0,
                };
            }

            string folderName = string.IsNullOrWhiteSpace(request.FolderName)
                ? request.Kind == ExportDataKind.Score
                    ? $"replays-{DateTime.Now:yyyyMMdd_HHmmss}"
                    : $"songs-{DateTime.Now:yyyyMMdd_HHmmss}"
                : request.FolderName.Trim();

            string outputRoot = Path.Combine(request.OutputDirectory, folderName);
            Directory.CreateDirectory(outputRoot);

            int exported = 0;
            int index = 0;

            if (request.Kind == ExportDataKind.Collection)
            {
                var snapshot = await ensureLoadedAsync(request.RealmId, progress, cancellationToken).ConfigureAwait(false);

                foreach (var collectionRow in catalog.Items.Where(i => idSet.Contains(i.Id)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    index++;
                    string collectionDir = Path.Combine(outputRoot, sanitizePathSegment(collectionRow.Title));

                    foreach (var beatmap in snapshot.Groups.First(g => g.EntityKind == EntityKind.Beatmap).Rows)
                    {
                        string relative = $"{beatmap.Artist} - {beatmap.Title}/{beatmap.Ruleset}.osu";
                        string destPath = Path.Combine(collectionDir, relative);
                        string? destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir))
                            Directory.CreateDirectory(destDir);

                        string sourcePath = Path.Combine(request.FilesDirectory, relative);
                        if (File.Exists(sourcePath))
                            File.Copy(sourcePath, destPath, overwrite: true);
                        else
                            await File.WriteAllTextAsync(destPath, $"// mock export\n// {beatmap.Artist} - {beatmap.Title}", cancellationToken).ConfigureAwait(false);

                        exported++;
                    }
                }

                await simulateWorkAsync(progress, "导出完成", cancellationToken).ConfigureAwait(false);

                return new RealmExportResult
                {
                    OutputRoot = outputRoot,
                    ExportedCount = exported,
                    SkippedCount = 0,
                };
            }

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

                string destRelative = string.IsNullOrWhiteSpace(item.DestinationRelativePath)
                    ? item.RelativePath
                    : item.DestinationRelativePath;

                if (request.Kind == ExportDataKind.Score && request.GroupScoresByPlayer && !string.IsNullOrWhiteSpace(item.PlayerName))
                    destRelative = Path.Combine("replays", sanitizePathSegment(item.PlayerName), Path.GetFileName(destRelative) ?? destRelative);
                else if (request.Kind == ExportDataKind.Score && !request.GroupScoresByPlayer)
                    destRelative = Path.Combine("replays", Path.GetFileName(destRelative) ?? destRelative);

                string destPath = Path.Combine(outputRoot, destRelative);
                string? destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);

                string sourcePath = Path.Combine(request.FilesDirectory, item.RelativePath);
                if (File.Exists(sourcePath))
                    File.Copy(sourcePath, destPath, overwrite: true);
                else
                    await File.WriteAllTextAsync(destPath, $"// mock export\n// {item.Title}", cancellationToken).ConfigureAwait(false);

                exported++;
            }

            await simulateWorkAsync(progress, "导出完成", cancellationToken).ConfigureAwait(false);

            return new RealmExportResult
            {
                OutputRoot = outputRoot,
                ExportedCount = exported,
                SkippedCount = Math.Max(0, idSet.Count - exported),
            };
        }

        public Task<RealmExportResult> ExportBrowseEntitiesAsync(
            string realmId,
            string filesDirectory,
            RealmObjectClass objectClass,
            IReadOnlyList<Guid> entityIds,
            string outputDirectory,
            string? folderName = null,
            bool groupScoresByPlayer = true,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) => ExportAsync(
            new RealmExportRequest
            {
                RealmId = realmId,
                    Kind = objectClass switch
                    {
                        RealmObjectClass.BeatmapCollection => ExportDataKind.Collection,
                        RealmObjectClass.Score => ExportDataKind.Score,
                        RealmObjectClass.Beatmap => ExportDataKind.Beatmap,
                        _ => ExportDataKind.BeatmapSet,
                    },
                FilesDirectory = filesDirectory,
                OutputDirectory = outputDirectory,
                FolderName = folderName,
                ItemIds = entityIds,
                GroupScoresByPlayer = groupScoresByPlayer,
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
