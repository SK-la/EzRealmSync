#if HAS_EZ_OSU_GAME
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.EzRealmSync.Models;
using osu.Game.Scoring;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    internal readonly struct RealmExportFileEntry
    {
        public string SourceRelative { get; init; }
        public string DestinationRelative { get; init; }
        public string? CollectionFolder { get; init; }
    }

    internal static class RealmExportExecutor
    {
        public static IReadOnlyList<RealmExportFileEntry> ResolveFiles(
            RealmAccess access,
            ExportDataKind kind,
            IReadOnlyCollection<Guid> selectedIds,
            bool groupScoresByPlayer)
        {
            var idSet = selectedIds as HashSet<Guid> ?? selectedIds.ToHashSet();
            var entries = new List<RealmExportFileEntry>();

            access.Run(realm =>
            {
                switch (kind)
                {
                    case ExportDataKind.Collection:
                        expandCollections(realm, idSet, entries);
                        break;

                    default:
                        throw new InvalidOperationException($"ResolveFiles 不支持 {kind}，请使用目录项直接复制。");
                }
            });

            return entries;
        }

        public static RealmExportFileEntry CreateScoreEntry(ScoreInfo score, bool groupScoresByPlayer)
        {
            var replay = score.Files.FirstOrDefault(f => f.Filename.EndsWith(".osr", StringComparison.OrdinalIgnoreCase));
            if (replay == null)
                throw new InvalidOperationException("成绩缺少 .osr 文件引用。");

            string source = RealmFilePathHelper.GetStoragePath(replay.File.Hash);
            string fileName = $"{score.GetDisplayString().GetValidFilename()} ({score.Date.LocalDateTime:yyyy-MM-dd_HH-mm}).osr";
            string playerFolder = sanitizePathSegment(score.RealmUser.Username);

            string destRelative = groupScoresByPlayer && !string.IsNullOrWhiteSpace(playerFolder)
                ? Path.Combine("replays", playerFolder, fileName)
                : Path.Combine("replays", fileName);

            return new RealmExportFileEntry
            {
                SourceRelative = source,
                DestinationRelative = destRelative,
            };
        }

        public static RealmExportFileEntry CreateBeatmapEntry(string beatmapHash, string? collectionFolder = null)
        {
            string relative = RealmFilePathHelper.GetStoragePath(beatmapHash);
            return new RealmExportFileEntry
            {
                SourceRelative = relative,
                DestinationRelative = relative,
                CollectionFolder = collectionFolder,
            };
        }

        private static void expandCollections(RealmInstance realm, HashSet<Guid> idSet, List<RealmExportFileEntry> entries)
        {
            var beatmapsByMd5 = realm.All<BeatmapInfo>()
                                     .Where(b => b.BeatmapSet == null || !b.BeatmapSet.DeletePending)
                                     .AsEnumerable()
                                     .GroupBy(b => b.MD5Hash, StringComparer.Ordinal)
                                     .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            foreach (var collection in realm.All<BeatmapCollection>())
            {
                if (!idSet.Contains(collection.ID))
                    continue;

                string folder = sanitizePathSegment(collection.Name);
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (string md5 in collection.BeatmapMD5Hashes)
                {
                    if (!beatmapsByMd5.TryGetValue(md5, out var beatmap))
                        continue;

                    string relative = RealmFilePathHelper.GetStoragePath(beatmap.Hash);
                    if (!seen.Add(relative))
                        continue;

                    entries.Add(new RealmExportFileEntry
                    {
                        SourceRelative = relative,
                        DestinationRelative = relative,
                        CollectionFolder = folder,
                    });
                }
            }
        }

        public static void CopyEntry(
            RealmExportFileEntry entry,
            string filesDirectory,
            string outputRoot,
            ExportDataKind kind)
        {
            string targetDir = kind == ExportDataKind.Collection && !string.IsNullOrEmpty(entry.CollectionFolder)
                ? Path.Combine(outputRoot, entry.CollectionFolder!)
                : outputRoot;

            string destPath = Path.Combine(targetDir, entry.DestinationRelative);
            string? destDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);

            string sourcePath = Path.Combine(filesDirectory, entry.SourceRelative);
            if (File.Exists(sourcePath))
                File.Copy(sourcePath, destPath, overwrite: true);
        }

        public static string SanitizePathSegment(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            return name.Trim();
        }

        private static string sanitizePathSegment(string name) => SanitizePathSegment(name);
    }
}
#endif
