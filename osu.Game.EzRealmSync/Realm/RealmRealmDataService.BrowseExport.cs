using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm.Dynamic;
using Realms;

namespace osu.Game.EzRealmSync.Realm
{
    public sealed partial class RealmRealmDataService
    {
        public Task<RealmExportResult> ExportBrowseEntitiesAsync(
            string realmId,
            string filesDirectory,
            RealmObjectClass objectClass,
            IReadOnlyList<Guid> entityIds,
            string outputDirectory,
            string? folderName = null,
            bool groupScoresByPlayer = true,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.Run(() => exportBrowseCore(realmId, filesDirectory, objectClass, entityIds, outputDirectory, folderName, groupScoresByPlayer, progress, cancellationToken), cancellationToken);

        /// <summary>
        /// 数据页导出：按选中行收集「源文件相对路径 → 目标相对路径」，再逐个复制。
        ///
        /// 全程动态读（<c>RealmFilePathHelper.GetStoragePath</c> 只吃 hash，不需要模型），
        /// 所以旧库 / 官方库同样能导出，不要求磁盘 schema 能被当前模型打开。
        /// </summary>
        private RealmExportResult exportBrowseCore(
            string realmId,
            string filesDirectory,
            RealmObjectClass objectClass,
            IReadOnlyList<Guid> entityIds,
            string outputDirectory,
            string? folderName,
            bool groupScoresByPlayer,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (!RealmBrowseEntityMutator.SupportsFileExport(objectClass))
                throw new InvalidOperationException($"类型 {objectClass} 暂不支持从数据页导出文件。");

            if (entityIds.Count == 0)
                return new RealmExportResult();

            if (!registry.TryGet(realmId, out var file))
                throw new InvalidOperationException($"未找到 Realm 文件：{realmId}");

            string folder = string.IsNullOrWhiteSpace(folderName)
                ? $"browse-export-{DateTime.Now:yyyyMMdd_HHmmss}"
                : folderName.Trim();

            string outputRoot = Path.Combine(outputDirectory, folder);
            Directory.CreateDirectory(outputRoot);

            var relativePaths = new List<(string sourceRelative, string destRelative, string? subDir)>();

            using (var session = RealmAccessGateway.OpenDynamicForRead(file.FilePath, out RealmSchemaSnapshot schema))
            {
                foreach (Guid id in entityIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    collectExportPaths(session, schema, objectClass, id, relativePaths, groupScoresByPlayer);
                }
            }

            int exported = 0;
            int skipped = 0;
            int index = 0;

            foreach (var (sourceRelative, destRelative, subDir) in relativePaths.Distinct())
            {
                cancellationToken.ThrowIfCancellationRequested();
                index++;

                progress?.Report(new ScanProgress
                {
                    Progress = (double)index / Math.Max(1, relativePaths.Count),
                    Message = destRelative,
                });

                string targetDir = string.IsNullOrEmpty(subDir) ? outputRoot : Path.Combine(outputRoot, DynamicDisplayText.SanitizePathSegment(subDir));
                string destPath = Path.Combine(targetDir, destRelative);
                string? destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);

                string sourcePath = Path.Combine(filesDirectory, sourceRelative);

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

        private static void collectExportPaths(
            DynamicRealmSession session,
            RealmSchemaSnapshot schema,
            RealmObjectClass objectClass,
            Guid id,
            List<(string sourceRelative, string destRelative, string? subDir)> paths,
            bool groupScoresByPlayer)
        {
            switch (objectClass)
            {
                case RealmObjectClass.BeatmapSet:
                {
                    // 谱面集自己没有文件列：导出集就是导出它下面所有难度的 .osu。
                    if (DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.BeatmapSet, id) is { } set)
                    {
                        foreach (IRealmObjectBase beatmap in DynamicRealmAccess.EnumerateObjects(set, "Beatmaps"))
                            addBeatmapPath(paths, DynamicRowAccess.ResolveString(beatmap, schema, "Hash"));
                    }

                    break;
                }

                case RealmObjectClass.Beatmap:
                {
                    if (DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.Beatmap, id) is { } beatmap)
                        addBeatmapPath(paths, DynamicRowAccess.ResolveString(beatmap, schema, "Hash"));

                    break;
                }

                case RealmObjectClass.BeatmapCollection:
                {
                    if (DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.BeatmapCollection, id) is { } collection)
                    {
                        string subDir = DynamicRowAccess.ResolveString(collection, schema, "Name") ?? string.Empty;

                        // 收藏夹存的是难度 MD5，不是文件 hash：要经 Beatmap 表翻译成 blob 路径。
                        var md5ToHash = buildMd5ToHash(session, schema);

                        foreach (string md5 in DynamicRealmAccess.EnumerateValues<string>(collection, "BeatmapMD5Hashes"))
                        {
                            if (md5ToHash.TryGetValue(md5, out string? hash))
                                addBeatmapPath(paths, hash, subDir);
                        }
                    }

                    break;
                }

                case RealmObjectClass.Score:
                {
                    if (DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.Score, id) is { } score)
                        addScorePath(paths, score, schema, groupScoresByPlayer);

                    break;
                }
            }
        }

        /// <summary>收藏夹展开时的 MD5 → 文件 hash 映射；与旧 typed 版本一致，软删谱面不参与。</summary>
        private static Dictionary<string, string> buildMd5ToHash(DynamicRealmSession session, RealmSchemaSnapshot schema)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (IRealmObjectBase beatmap in DynamicRowAccess.AllRows(session, schema, OfficialBaselineSchema.Beatmap))
            {
                if (DynamicRowAccess.ResolveBool(beatmap, schema, "BeatmapSet.DeletePending"))
                    continue;

                string? md5 = DynamicRowAccess.ResolveString(beatmap, schema, "MD5Hash");
                string? hash = DynamicRowAccess.ResolveString(beatmap, schema, "Hash");

                if (!string.IsNullOrEmpty(md5) && !string.IsNullOrEmpty(hash))
                    map.TryAdd(md5, hash);
            }

            return map;
        }

        private static void addBeatmapPath(List<(string sourceRelative, string destRelative, string? subDir)> paths, string? beatmapHash, string? subDir = null)
        {
            if (string.IsNullOrWhiteSpace(beatmapHash))
                return;

            string relative = RealmFilePathHelper.GetStoragePath(beatmapHash);
            paths.Add((relative, relative, subDir));
        }

        private static void addScorePath(
            List<(string sourceRelative, string destRelative, string? subDir)> paths,
            IRealmObjectBase score,
            RealmSchemaSnapshot schema,
            bool groupScoresByPlayer)
        {
            // 没有 .osr 引用的成绩导不出回放文件；旧 typed 版本靠异常吞掉，这里显式跳过。
            if (DynamicExportCatalogBuilder.TryCreateScoreEntry(score, schema, groupScoresByPlayer) is not { } entry)
                return;

            paths.Add((entry.SourceRelative, entry.DestinationRelative, null));
        }
    }
}
