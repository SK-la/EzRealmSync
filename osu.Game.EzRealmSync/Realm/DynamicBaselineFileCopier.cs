using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>按官方 File.hash 复制缺失的 files/ blob。同一数据根是 no-op。</summary>
    public static class DynamicBaselineFileCopier
    {
        public static int CopyMissing(string? sourceRealmPath, string? targetRealmPath, RealmSyncApplyBundle bundle)
        {
            if (string.IsNullOrWhiteSpace(sourceRealmPath) || string.IsNullOrWhiteSpace(targetRealmPath))
                return 0;

            if (!RealmWorkspacePaths.TryResolveFilesDirectory(RealmWorkspacePaths.ResolveStorageRoot(sourceRealmPath), out string sourceFiles))
                return 0;

            if (!RealmWorkspacePaths.TryResolveFilesDirectory(RealmWorkspacePaths.ResolveStorageRoot(targetRealmPath), out string targetFiles))
                return 0;

            if (string.Equals(Path.GetFullPath(sourceFiles), Path.GetFullPath(targetFiles), StringComparison.OrdinalIgnoreCase))
                return 0;

            int copied = 0;

            foreach (string hash in enumerateHashes(bundle))
            {
                string source = RealmFilePathHelper.GetFullPath(sourceFiles, hash);
                string destination = RealmFilePathHelper.GetFullPath(targetFiles, hash);

                if (!File.Exists(source) || File.Exists(destination))
                    continue;

                string? directory = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.Copy(source, destination, overwrite: false);
                copied++;
            }

            return copied;
        }

        private static IEnumerable<string> enumerateHashes(RealmSyncApplyBundle bundle)
        {
            var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var set in bundle.BeatmapSets)
            {
                add(hashes, set.Files);
                add(hashes, set.Hash);
            }

            foreach (var beatmap in bundle.Beatmaps)
                add(hashes, beatmap.Hash);

            foreach (var skin in bundle.Skins)
                add(hashes, skin.Files);

            foreach (var score in bundle.Scores)
                add(hashes, score.Files);

            return hashes;
        }

        private static void add(HashSet<string> hashes, IEnumerable<OfficialNamedFileDto> files)
        {
            foreach (var file in files)
                add(hashes, file.Hash);
        }

        private static void add(HashSet<string> hashes, string? hash)
        {
            if (!string.IsNullOrWhiteSpace(hash))
                hashes.Add(hash);
        }
    }
}
