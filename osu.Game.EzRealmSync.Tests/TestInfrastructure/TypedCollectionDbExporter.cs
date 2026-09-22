#if HAS_EZ_OSU_GAME
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.EzRealmSync.IO;

namespace osu.Game.EzRealmSync.Tests.TestInfrastructure
{
    /// <summary>
    /// collection.db 导出的 **typed 参考实现**，只给对照测试用。
    ///
    /// 产品侧已改成动态读取（<see cref="Realm.RealmCollectionDbSync"/>），这里保留一份走 Os 模型的写法，
    /// 用来证明动态版写出的字节与官方模型读出来的内容完全一致——字节对照比字段对照更能抓住
    /// 「字段都对但顺序/编码错了」这类问题。
    /// </summary>
    internal static class TypedCollectionDbExporter
    {
        public static int Export(RealmAccess access, IReadOnlyCollection<Guid> selectedIds, string outputFile)
        {
            var idSet = selectedIds as HashSet<Guid> ?? selectedIds.ToHashSet();
            var entries = new List<LegacyCollectionDbEntry>();

            access.Run(realm =>
            {
                foreach (var collection in realm.All<BeatmapCollection>())
                {
                    if (idSet.Count > 0 && !idSet.Contains(collection.ID))
                        continue;

                    entries.Add(new LegacyCollectionDbEntry(
                        collection.Name,
                        collection.BeatmapMD5Hashes.ToList()));
                }
            });

            LegacyCollectionDb.WriteFile(outputFile, entries);
            return entries.Count;
        }
    }
}
#endif
