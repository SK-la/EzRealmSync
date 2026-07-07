#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.IO;
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

        public Task<RealmOfficialConversionResult> ConvertToOfficialRealmAsync(
            string realmId,
            string? outputRealmFilePath = null,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.Run(() => convertToOfficialCore(realmId, outputRealmFilePath, progress, cancellationToken), cancellationToken);

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

            // 综合并发检查：重试进程检测 + 排他文件锁
            string? guardError = Task.Run(() => RealmProcessGuard.ComprehensiveCheckAsync(file.FilePath)).GetAwaiter().GetResult();
            if (guardError != null)
                throw new InvalidOperationException(guardError);

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
            invalidateAfterMutatingRealm(realmId, file.FilePath);
            progress?.Report(new ScanProgress { Progress = 1, Message = $"已处理 {applied} 项" });

            return new RealmFixApplyResult { AppliedCount = applied, SkippedCount = skipped };
        }

        private RealmOfficialConversionResult convertToOfficialCore(
            string realmId,
            string? outputRealmFilePath,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (!registry.TryGet(realmId, out var file))
                throw new InvalidOperationException($"未找到 Realm 文件：{realmId}");

            if (RealmSchemaSafety.Classify(file.SchemaVersion) != RealmDiskSchemaKind.EzExtended)
                throw new InvalidOperationException("所选库不是 Ez 扩展库，无需“转回官方版”。");

            string sourcePath = Path.GetFullPath(file.FilePath);
            string sourceName = Path.GetFileName(sourcePath);
            if (!string.IsNullOrWhiteSpace(outputRealmFilePath)
                && !string.Equals(Path.GetFullPath(outputRealmFilePath), sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("“转回官方版”仅支持原地转换：会先自动备份，再覆盖所选 Realm 文件本身。");
            }
            string? guardError = Task.Run(() => RealmProcessGuard.ComprehensiveCheckAsync(sourcePath)).GetAwaiter().GetResult();
            if (guardError != null)
                throw new InvalidOperationException(guardError);

            progress?.Report(new ScanProgress { Progress = 0.05, Message = "正在创建自动备份…" });
            string backupPath = RealmFileBackup.CreateTimestampedCopy(sourcePath, EzRealmSyncDefaults.DefaultBackupDirectory);

            string tempRoot = Path.Combine(Path.GetTempPath(), "EzRealmSync", "official-convert", Guid.NewGuid().ToString("N"));
            string tempTargetPath = Path.Combine(tempRoot, sourceName);
            Directory.CreateDirectory(tempRoot);

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                progress?.Report(new ScanProgress { Progress = 0.12, Message = "正在创建官方目标库…" });
                createEmptyOfficialRealm(tempTargetPath);

                progress?.Report(new ScanProgress { Progress = 0.2, Message = "正在读取污染库内容…" });
                using var sourceAccess = RealmSchemaProbe.Open(sourcePath, file.SchemaVersion);
                var sourceSnapshot = RealmDiffReader.Read(sourceAccess, progress, cancellationToken);
                var itemIds = sourceSnapshot.Entities.Select(e => e.Id).Distinct().ToList();

                int targetSchema = RealmSchemaProbe.TryReadSchemaVersion(tempTargetPath) ?? RealmAccess.UpstreamSchemaVersion;
                var writePlan = new RealmWritePlan
                {
                    SourceRealmFilePath = sourcePath,
                    TargetRealmFilePath = tempTargetPath,
                    SourceKind = RealmDiskSchemaKind.EzExtended,
                    TargetKind = RealmDiskSchemaKind.PpyClient,
                    SourceSchemaVersion = file.SchemaVersion,
                    TargetSchemaVersion = targetSchema,
                    LegacyDirection = SyncDirection.EzToOfficial,
                };

                var request = new ApplyRequest
                {
                    WritePlan = writePlan,
                    Direction = SyncDirection.EzToOfficial,
                    ItemIds = itemIds,
                    CreateBackup = false,
                    DeleteFromSource = false,
                };

                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new ScanProgress { Progress = 0.4, Message = "正在写入官方数据…" });
                using var targetAccess = RealmDiffReader.OpenOfficialRealm(tempTargetPath, targetSchema);
                var apply = RealmRowCopier.Apply(
                    request,
                    sourceAccess,
                    targetAccess,
                    progress == null
                        ? null
                        : new Progress<ApplyProgress>(p => progress.Report(new ScanProgress
                        {
                            Progress = 0.4 + p.Progress * 0.45,
                            Message = p.Message,
                        })),
                    cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new ScanProgress { Progress = 0.9, Message = "正在覆盖原文件…" });
                File.Copy(tempTargetPath, sourcePath, overwrite: true);

                invalidateAfterMutatingRealm(realmId, sourcePath);

                progress?.Report(new ScanProgress { Progress = 1, Message = "转换完成" });

                return new RealmOfficialConversionResult
                {
                    TargetRealmFilePath = sourcePath,
                    AppliedCount = apply.AppliedCount,
                    BackupPath = backupPath,
                };
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
        }

        private static void createEmptyOfficialRealm(string targetPath)
        {
            string root = RealmWorkspacePaths.ResolveStorageRoot(targetPath);
            string fileName = Path.GetFileName(targetPath);

            Directory.CreateDirectory(root);

            using var access = new OfficialRealmAccess(new osu.Framework.Platform.NativeStorage(root), fileName, allowDestructiveRecoveryOnSchemaMismatch: false);
            access.Run(_ => { });
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
