#if HAS_EZ_OSU_GAME
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.EzRealmSync.Models;
using osu.Game.Extensions;
using osu.Game.Scoring;
using RealmInstance = Realms.Realm;

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
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.Run(() => exportBrowseCore(realmId, filesDirectory, objectClass, entityIds, outputDirectory, folderName, progress, cancellationToken), cancellationToken);

        private RealmExportResult exportBrowseCore(
            string realmId,
            string filesDirectory,
            RealmObjectClass objectClass,
            IReadOnlyList<Guid> entityIds,
            string outputDirectory,
            string? folderName,
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

            using (var access = RealmSchemaProbe.Open(file.FilePath, file.SchemaVersion))
            {
                access.Run(realm =>
                {
                    foreach (var id in entityIds)
                        collectExportPaths(realm, objectClass, id, relativePaths);
                });
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

                string targetDir = string.IsNullOrEmpty(subDir) ? outputRoot : Path.Combine(outputRoot, sanitizePathSegment(subDir));
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
            RealmInstance realm,
            RealmObjectClass objectClass,
            Guid id,
            List<(string sourceRelative, string destRelative, string? subDir)> paths)
        {
            switch (objectClass)
            {
                case RealmObjectClass.BeatmapSet:
                    if (realm.Find<BeatmapSetInfo>(id) is BeatmapSetInfo set)
                    {
                        foreach (var beatmap in set.Beatmaps)
                            addBeatmapPath(paths, beatmap.Hash, null);
                    }

                    break;

                case RealmObjectClass.BeatmapCollection:
                    if (realm.Find<BeatmapCollection>(id) is BeatmapCollection collection)
                    {
                        string subDir = collection.Name;

                        foreach (string md5 in collection.BeatmapMD5Hashes)
                        {
                            var beatmap = realm.All<BeatmapInfo>().FirstOrDefault(b => b.MD5Hash == md5);
                            if (beatmap != null)
                                addBeatmapPath(paths, beatmap.Hash, subDir);
                        }
                    }

                    break;

                case RealmObjectClass.Score:
                    if (realm.Find<ScoreInfo>(id) is ScoreInfo score)
                        addScorePath(paths, score);

                    break;
            }
        }

        private static void addBeatmapPath(List<(string sourceRelative, string destRelative, string? subDir)> paths, string beatmapHash, string? subDir)
        {
            if (string.IsNullOrWhiteSpace(beatmapHash))
                return;

            string relative = RealmFilePathHelper.GetStoragePath(beatmapHash);
            paths.Add((relative, relative, subDir));
        }

        private static void addScorePath(List<(string sourceRelative, string destRelative, string? subDir)> paths, ScoreInfo score)
        {
            var replay = score.Files.FirstOrDefault(f => f.Filename.EndsWith(".osr", StringComparison.OrdinalIgnoreCase));
            if (replay == null)
                return;

            string source = RealmFilePathHelper.GetStoragePath(replay.File.Hash);
            string destName = $"{score.GetDisplayString().GetValidFilename()} ({score.Date.LocalDateTime:yyyy-MM-dd_HH-mm}).osr";
            paths.Add((source, Path.Combine("replays", destName), null));
        }
    }
}
#endif
