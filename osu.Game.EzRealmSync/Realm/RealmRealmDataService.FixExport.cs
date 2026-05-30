#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;
using osu.Game.Scoring;

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

        private RealmExportCatalog loadCatalogCore(
            string realmId,
            ExportDataKind kind,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            var key = (realmId, kind);
            if (exportCatalogs.TryGetValue(key, out var cached))
                return cached;

            if (!registry.TryGet(realmId, out var file))
                throw new InvalidOperationException($"未找到 Realm 文件：{realmId}");

            progress?.Report(new ScanProgress { Progress = 0, Message = "正在打开 Realm…" });
            using var access = RealmSchemaProbe.Open(file.FilePath, file.SchemaVersion);
            var catalog = RealmExportCatalogBuilder.Build(access, kind, progress, cancellationToken);
            exportCatalogs[key] = catalog;
            return catalog;
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
            if (!registry.TryGet(request.RealmId, out var file))
                throw new InvalidOperationException($"未找到 Realm 文件：{request.RealmId}");

            string folderName = string.IsNullOrWhiteSpace(request.FolderName)
                ? defaultExportFolderName(request.Kind)
                : request.FolderName.Trim();

            string outputRoot = Path.Combine(request.OutputDirectory, folderName);
            Directory.CreateDirectory(outputRoot);

            var idSet = request.ItemIds.ToHashSet();
            int exported = 0;
            int skipped = 0;

            if (request.Kind is ExportDataKind.Collection or ExportDataKind.Score)
            {
                using var access = RealmSchemaProbe.Open(file.FilePath, file.SchemaVersion);
                var entries = request.Kind == ExportDataKind.Collection
                    ? RealmExportExecutor.ResolveFiles(access, request.Kind, idSet, request.GroupScoresByPlayer)
                    : resolveScoreEntries(access, idSet, request.GroupScoresByPlayer);

                int index = 0;

                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    index++;
                    progress?.Report(new ScanProgress
                    {
                        Progress = (double)index / Math.Max(1, entries.Count),
                        Message = entry.DestinationRelative,
                    });

                    if (tryCopyEntry(entry, request.FilesDirectory, outputRoot, request.Kind))
                        exported++;
                    else
                        skipped++;
                }
            }
            else
            {
                if (!exportCatalogs.TryGetValue((request.RealmId, request.Kind), out var catalog))
                    catalog = loadCatalogCore(request.RealmId, request.Kind, progress, cancellationToken);

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

                    var entry = new RealmExportFileEntry
                    {
                        SourceRelative = item.RelativePath,
                        DestinationRelative = string.IsNullOrWhiteSpace(item.DestinationRelativePath) ? item.RelativePath : item.DestinationRelativePath,
                    };

                    if (tryCopyEntry(entry, request.FilesDirectory, outputRoot, request.Kind))
                        exported++;
                    else
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

        private static List<RealmExportFileEntry> resolveScoreEntries(RealmAccess access, HashSet<Guid> idSet, bool groupScoresByPlayer)
        {
            var entries = new List<RealmExportFileEntry>();

            access.Run(realm =>
            {
                foreach (var score in realm.All<ScoreInfo>().Where(s => !s.DeletePending && idSet.Contains(s.ID)))
                {
                    try
                    {
                        entries.Add(RealmExportExecutor.CreateScoreEntry(score, groupScoresByPlayer));
                    }
                    catch (InvalidOperationException)
                    {
                        // 无 .osr 引用则跳过
                    }
                }
            });

            return entries;
        }

        private static bool tryCopyEntry(RealmExportFileEntry entry, string filesDirectory, string outputRoot, ExportDataKind kind)
        {
            string targetDir = kind == ExportDataKind.Collection && !string.IsNullOrEmpty(entry.CollectionFolder)
                ? Path.Combine(outputRoot, entry.CollectionFolder!)
                : outputRoot;

            string destPath = Path.Combine(targetDir, entry.DestinationRelative);
            string? destDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);

            string sourcePath = Path.Combine(filesDirectory, entry.SourceRelative);
            if (!File.Exists(sourcePath))
                return false;

            File.Copy(sourcePath, destPath, overwrite: true);
            return true;
        }

        private static string defaultExportFolderName(ExportDataKind kind) => kind switch
        {
            ExportDataKind.Score => $"replays-{DateTime.Now:yyyyMMdd_HHmmss}",
            _ => $"songs-{DateTime.Now:yyyyMMdd_HHmmss}",
        };
    }
}
#endif
