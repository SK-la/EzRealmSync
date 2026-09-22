#if HAS_EZ_OSU_GAME
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.EzRealmSync.Models;
using osu.Game.Scoring;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Tests.TestInfrastructure
{
    /// <summary>
    /// 收藏夹展开 / 成绩导出条目的 **typed 参考实现**，只给对照测试用（产品侧已改为动态读取）。
    ///
    /// 与旧产品实现只有一处差别：筛选「未软删谱面」的查询先 <c>AsEnumerable()</c> 再过滤。
    /// 原来的 <c>realm.All&lt;BeatmapInfo&gt;().Where(b =&gt; !b.BeatmapSet.DeletePending)</c> 表达式
    /// Realm 的 LINQ provider 翻译不了，会在枚举时抛 <c>NotSupportedException</c>——以前没人跑到是因为
    /// 样本里没有收藏夹，展开逻辑从未执行。这里保留参考语义，不在测试夹具里重演那个查询缺陷。
    /// </summary>
    internal static class TypedExportExecutor
    {
        public static IReadOnlyList<RealmExportFileEntry> ResolveCollectionFiles(
            RealmAccess access,
            IReadOnlyCollection<Guid> selectedIds)
        {
            var idSet = selectedIds as HashSet<Guid> ?? selectedIds.ToHashSet();
            var entries = new List<RealmExportFileEntry>();

            access.Run(realm => expandCollections(realm, idSet, entries));
            return entries;
        }

        public static RealmExportFileEntry CreateScoreEntry(ScoreInfo score, bool groupScoresByPlayer)
        {
            var replay = score.Files.FirstOrDefault(f => f.Filename.EndsWith(".osr", StringComparison.OrdinalIgnoreCase));
            if (replay == null)
                throw new InvalidOperationException("成绩缺少 .osr 文件引用。");

            string source = RealmFilePathHelper.GetStoragePath(replay.File.Hash);
            string fileName = $"{score.GetDisplayString().GetValidFilename()} ({score.Date.LocalDateTime:yyyy-MM-dd_HH-mm}).osr";
            string playerFolder = SanitizePathSegment(score.RealmUser.Username);

            string destRelative = groupScoresByPlayer && !string.IsNullOrWhiteSpace(playerFolder)
                ? Path.Combine("replays", playerFolder, fileName)
                : Path.Combine("replays", fileName);

            return new RealmExportFileEntry
            {
                SourceRelative = source,
                DestinationRelative = destRelative,
            };
        }

        private static void expandCollections(RealmInstance realm, HashSet<Guid> idSet, List<RealmExportFileEntry> entries)
        {
            var beatmapsByMd5 = realm.All<BeatmapInfo>()
                                     .AsEnumerable()
                                     .Where(b => b.BeatmapSet == null || !b.BeatmapSet.DeletePending)
                                     .GroupBy(b => b.MD5Hash, StringComparer.Ordinal)
                                     .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            foreach (var collection in realm.All<BeatmapCollection>())
            {
                if (!idSet.Contains(collection.ID))
                    continue;

                string folder = SanitizePathSegment(collection.Name);
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

        public static string SanitizePathSegment(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            return name.Trim();
        }
    }
}
#endif
