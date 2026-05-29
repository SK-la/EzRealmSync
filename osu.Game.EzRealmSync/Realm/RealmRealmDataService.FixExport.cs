#if HAS_EZ_OSU_GAME
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    public sealed partial class RealmRealmDataService
    {
        private readonly Dictionary<string, List<RealmFixIssue>> fixIssuesByRealm = new();
        private readonly Dictionary<(string realmId, ExportDataKind kind), RealmExportCatalog> exportCatalogs = new();

        public Task<IReadOnlyList<RealmFixIssue>> ScanIssuesAsync(
            string realmId,
            string workspacePath,
            RealmFixScanOptions options,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) => Task.Run(() => scanIssuesCore(realmId, workspacePath, options, progress, cancellationToken), cancellationToken);

        private IReadOnlyList<RealmFixIssue> scanIssuesCore(
            string realmId,
            string workspacePath,
            RealmFixScanOptions options,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (!registry.TryGet(realmId, out var file))
                throw new InvalidOperationException($"未找到 Realm 文件：{realmId}");

            if (!RealmWorkspaceDiscovery.TryResolveSharedFilesDirectory(workspacePath, out string filesDirectory))
                throw new InvalidOperationException("未找到共享 files/ 目录。请在导入页选择 osu! 数据根目录。");

            var issues = new List<RealmFixIssue>();

            progress?.Report(new ScanProgress { Progress = 0, Message = "正在打开 Realm…" });
            using var access = RealmSchemaProbe.Open(file.FilePath, file.SchemaVersion);

            if (options.ScanMissingFiles)
            {
                progress?.Report(new ScanProgress { Progress = 0.2, Message = "正在检查缺失文件…" });
                RealmOrphanFileScanner.ScanMissingReferencedFiles(access, filesDirectory, issues, cancellationToken);
            }

            if (options.ScanOrphanFiles)
            {
                progress?.Report(new ScanProgress { Progress = 0.5, Message = "正在检查僵尸文件…" });
                RealmOrphanFileScanner.ScanOrphansOnDisk(access, filesDirectory, issues, cancellationToken);
            }

            if (options.ScanIllegalCharacters)
            {
                progress?.Report(new ScanProgress { Progress = 0.7, Message = "正在检查非法字符…" });
                RealmIllegalCharacterFixer.Scan(access, issues, options);
            }

            fixIssuesByRealm[realmId] = issues;
            progress?.Report(new ScanProgress { Progress = 1, Message = $"扫描完成（{issues.Count} 项）" });
            return issues;
        }

        public Task<RealmFixApplyResult> ApplyFixesAsync(
            string realmId,
            string workspacePath,
            IReadOnlyList<Guid> issueIds,
            RealmFixApplyOptions options,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) => Task.Run(() => applyFixesCore(realmId, issueIds, progress, cancellationToken), cancellationToken);

        private RealmFixApplyResult applyFixesCore(
            string realmId,
            IReadOnlyList<Guid> issueIds,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (!fixIssuesByRealm.TryGetValue(realmId, out var issues))
                return new RealmFixApplyResult();

            if (!registry.TryGet(realmId, out var file))
                throw new InvalidOperationException($"未找到 Realm 文件：{realmId}");

            string? processBlock = RealmProcessGuard.TryGetBlockingProcessMessage();
            if (processBlock != null)
                throw new InvalidOperationException(processBlock);

            var idSet = issueIds.ToHashSet();
            var selected = issues.Where(i => idSet.Contains(i.Id)).ToList();
            int applied = 0;
            int skipped = 0;

            progress?.Report(new ScanProgress { Progress = 0, Message = "正在写入 Realm…" });
            using var access = RealmSchemaProbe.Open(file.FilePath, file.SchemaVersion);

            var illegalIssues = selected.Where(i => i.Kind == RealmFixIssueKind.IllegalCharacter).ToList();

            if (illegalIssues.Count > 0)
            {
                applied += RealmIllegalCharacterFixer.Apply(access, illegalIssues, cancellationToken);
                snapshotCache.Remove(realmId);
            }

            foreach (var issue in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();

                switch (issue.Kind)
                {
                    case RealmFixIssueKind.MissingFile when issue.ExpectedFilePath != null:
                    {
                        string? dir = Path.GetDirectoryName(issue.ExpectedFilePath);
                        if (!string.IsNullOrEmpty(dir))
                            Directory.CreateDirectory(dir);

                        if (!File.Exists(issue.ExpectedFilePath))
                            File.WriteAllText(issue.ExpectedFilePath, string.Empty);

                        applied++;
                        break;
                    }

                    case RealmFixIssueKind.OrphanFile:
                        // 批量删除见下方
                        break;

                    case RealmFixIssueKind.IllegalCharacter:
                        if (!illegalIssues.Contains(issue))
                            skipped++;
                        break;

                    default:
                        skipped++;
                        break;
                }
            }

            int orphanDeleted = RealmOrphanFileScanner.DeleteOrphanFiles(selected);
            applied += orphanDeleted;

            fixIssuesByRealm[realmId] = issues.Where(i => !idSet.Contains(i.Id)).ToList();
            progress?.Report(new ScanProgress { Progress = 1, Message = $"已处理 {applied} 项" });

            return new RealmFixApplyResult { AppliedCount = applied, SkippedCount = skipped };
        }

        public Task<RealmExportCatalog> LoadCatalogAsync(
            string realmId,
            ExportDataKind kind,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) => Task.Run(() => loadCatalogCore(realmId, kind, progress, cancellationToken), cancellationToken);

        private RealmExportCatalog loadCatalogCore(
            string realmId,
            ExportDataKind kind,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            var key = (realmId, kind);
            if (exportCatalogs.TryGetValue(key, out var cached))
                return cached;

            var snapshot = loadCore(realmId, progress, cancellationToken);
            var items = new List<RealmExportItem>();

            switch (kind)
            {
                case ExportDataKind.BeatmapSet:
                    addBeatmapSetExportItems(snapshot, items);
                    break;

                case ExportDataKind.Beatmap:
                    addBeatmapExportItems(snapshot, items);
                    break;

                case ExportDataKind.Collection:
                    addCollectionExportItems(snapshot, items);
                    break;
            }

            var catalog = new RealmExportCatalog { Kind = kind, Items = items };
            exportCatalogs[key] = catalog;
            return catalog;
        }

        private static void addBeatmapSetExportItems(RealmSnapshot snapshot, List<RealmExportItem> items)
        {
            var beatmapRows = snapshot.Classes.FirstOrDefault(c => c.Class == RealmObjectClass.Beatmap)?.Rows ?? new List<RealmBrowseRow>();

            foreach (var setRow in snapshot.Classes.FirstOrDefault(c => c.Class == RealmObjectClass.BeatmapSet)?.Rows ?? new List<RealmBrowseRow>())
            {
                if (!setRow.Cells.TryGetValue("Hash", out string? setHash) || string.IsNullOrWhiteSpace(setHash))
                    continue;

                var firstBeatmap = beatmapRows.FirstOrDefault(b => b.Cells.TryGetValue("BeatmapSet", out string? bs) && string.Equals(bs, setHash, StringComparison.OrdinalIgnoreCase));

                string relative = firstBeatmap != null
                    ? buildBeatmapRelativePath(firstBeatmap, setHash)
                    : setHash;

                items.Add(new RealmExportItem
                {
                    Id = setRow.Id,
                    Title = setHash,
                    Artist = string.Empty,
                    RelativePath = relative,
                });
            }
        }

        private static void addBeatmapExportItems(RealmSnapshot snapshot, List<RealmExportItem> items)
        {
            foreach (var row in snapshot.Classes.FirstOrDefault(c => c.Class == RealmObjectClass.Beatmap)?.Rows ?? new List<RealmBrowseRow>())
            {
                string setHash = row.Cells.TryGetValue("BeatmapSet", out string? bs) ? bs : "unknown";
                items.Add(new RealmExportItem
                {
                    Id = row.Id,
                    Title = row.Cells.TryGetValue("Hash", out string? h) ? h : row.Id.ToString("N"),
                    Artist = string.Empty,
                    RelativePath = buildBeatmapRelativePath(row, setHash),
                });
            }
        }

        private static void addCollectionExportItems(RealmSnapshot snapshot, List<RealmExportItem> items)
        {
            var beatmapRows = snapshot.Classes.FirstOrDefault(c => c.Class == RealmObjectClass.Beatmap)?.Rows ?? new List<RealmBrowseRow>();

            foreach (var collection in snapshot.Classes.FirstOrDefault(c => c.Class == RealmObjectClass.BeatmapCollection)?.Rows ?? new List<RealmBrowseRow>())
            {
                string name = collection.Cells.TryGetValue("Name", out string? n) ? n : "Collection";

                foreach (var beatmap in beatmapRows.Take(8))
                {
                    string setHash = beatmap.Cells.TryGetValue("BeatmapSet", out string? bs) ? bs : "unknown";
                    items.Add(new RealmExportItem
                    {
                        Id = Guid.NewGuid(),
                        Title = beatmap.Cells.TryGetValue("Hash", out string? h) ? h : beatmap.Id.ToString("N"),
                        Artist = string.Empty,
                        CollectionName = name,
                        RelativePath = buildBeatmapRelativePath(beatmap, setHash),
                    });
                }
            }
        }

        private static string buildBeatmapRelativePath(RealmBrowseRow beatmapRow, string _)
        {
            string beatmapHash = beatmapRow.Cells.TryGetValue("Hash", out string? h) ? h : beatmapRow.Id.ToString("N");
            return RealmFilePathHelper.GetStoragePath(beatmapHash);
        }

        public Task<RealmExportResult> ExportAsync(
            RealmExportRequest request,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) => Task.Run(() => exportCore(request, progress, cancellationToken), cancellationToken);

        private RealmExportResult exportCore(
            RealmExportRequest request,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (!exportCatalogs.TryGetValue((request.RealmId, request.Kind), out var catalog))
                catalog = loadCatalogCore(request.RealmId, request.Kind, progress, cancellationToken);

            string folderName = string.IsNullOrWhiteSpace(request.FolderName)
                ? $"songs-{DateTime.Now:yyyyMMdd_HHmmss}"
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

                string destPath = Path.Combine(targetDir, item.RelativePath);
                string? destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);

                string sourcePath = Path.Combine(request.FilesDirectory, item.RelativePath);

                if (File.Exists(sourcePath))
                {
                    File.Copy(sourcePath, destPath, overwrite: true);
                    exported++;
                }
                else
                {
                    skipped++;
                }
            }

            progress?.Report(new ScanProgress { Progress = 1, Message = "导出完成" });

            return new RealmExportResult
            {
                OutputRoot = outputRoot,
                ExportedCount = exported,
                SkippedCount = skipped,
            };
        }

        private static string sanitizePathSegment(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            return name.Trim();
        }
    }
}
#endif
