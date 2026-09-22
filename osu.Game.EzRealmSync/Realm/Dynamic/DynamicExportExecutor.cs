using System.Collections;
using osu.Game.EzRealmSync.Models;
using Realms;

namespace osu.Game.EzRealmSync.Realm.Dynamic
{
    /// <summary>
    /// 导出时把「选中项」展开成待复制文件清单（动态读，不加载 osu.Game 模型）。
    /// </summary>
    internal static class DynamicExportExecutor
    {
        /// <summary>
        /// 收藏夹 → 其内含谱面的 <c>.osu</c> 文件。按 MD5 关联难度；找不到对应难度的 MD5 直接跳过
        /// （旧 typed 版本同样只导出库里有难度的条目），同一文件在一份收藏夹里只出现一次。
        /// </summary>
        public static IReadOnlyList<RealmExportFileEntry> ResolveCollectionFiles(
            DynamicRealmSession session,
            RealmSchemaSnapshot schema,
            IReadOnlyCollection<Guid> selectedIds)
        {
            var idSet = selectedIds as HashSet<Guid> ?? selectedIds.ToHashSet();

            var hashByMd5 = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (IRealmObjectBase beatmap in DynamicRowAccess.LiveRows(session, schema, OfficialBaselineSchema.Beatmap))
            {
                if (DynamicRowAccess.ResolveString(beatmap, schema, "MD5Hash") is not string md5 || string.IsNullOrEmpty(md5))
                    continue;

                if (DynamicRowAccess.ResolveString(beatmap, schema, "Hash") is not string hash || string.IsNullOrEmpty(hash))
                    continue;

                hashByMd5.TryAdd(md5, hash);
            }

            var entries = new List<RealmExportFileEntry>();

            foreach (IRealmObjectBase collection in DynamicRowAccess.AllRows(session, schema, OfficialBaselineSchema.BeatmapCollection))
            {
                if (DynamicRowAccess.Resolve(collection, schema, "ID") is not Guid id || !idSet.Contains(id))
                    continue;

                string folder = DynamicDisplayText.SanitizePathSegment(DynamicRowAccess.ResolveString(collection, schema, "Name") ?? string.Empty);
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (DynamicRowAccess.Resolve(collection, schema, "BeatmapMD5Hashes") is not IEnumerable hashes)
                    continue;

                foreach (object? value in hashes)
                {
                    if (value is not string md5 || !hashByMd5.TryGetValue(md5, out string? hash))
                        continue;

                    string relative = RealmFilePathHelper.GetStoragePath(hash);

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

            return entries;
        }
    }
}
