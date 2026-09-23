using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm.Dynamic;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 数据 Tab：对谱面集 / 成绩 / 收藏夹的动态删改（不触发 schema 迁移，不加载 osu.Game 模型）。
    ///
    /// 谱面集与成绩只置软删位；收藏夹没有软删语义，整行移除——与官方一致。
    /// </summary>
    public static class RealmBrowseEntityMutator
    {
        public static bool SupportsMutation(RealmObjectClass objectClass) => objectClass switch
        {
            RealmObjectClass.BeatmapSet => true,
            RealmObjectClass.Score => true,
            RealmObjectClass.BeatmapCollection => true,
            _ => false,
        };

        public static bool SupportsFileExport(RealmObjectClass objectClass) => objectClass switch
        {
            RealmObjectClass.BeatmapSet => true,
            RealmObjectClass.Beatmap => true,
            RealmObjectClass.BeatmapCollection => true,
            RealmObjectClass.Score => true,
            _ => false,
        };

        /// <summary>
        /// 返回**实际改动**的行数：库上没有软删列（更老的官方 schema）时按跳过处理，不假装删掉了。
        /// </summary>
        public static int Delete(DynamicRealmSession session, RealmSchemaSnapshot schema, RealmObjectClass objectClass, IReadOnlyList<Guid> ids)
        {
            if (!SupportsMutation(objectClass))
                throw new InvalidOperationException($"类型 {objectClass} 不支持从数据页删除。");

            string className = toClassName(objectClass);
            bool softDelete = objectClass != RealmObjectClass.BeatmapCollection;

            if (softDelete && !hasDeletePending(schema, className))
                return 0;

            int deleted = 0;

            using (var transaction = session.Realm.BeginWrite())
            {
                foreach (Guid id in ids)
                {
                    if (DynamicRealmAccess.Find(session.Realm, className, id) is not { } row)
                        continue;

                    if (softDelete)
                        DynamicRealmAccess.Set(row, className, "DeletePending", true);
                    else
                        session.Realm.Remove(row);

                    deleted++;
                }

                RealmSchemaDriftGuard.EnsureUnchanged(schema, DynamicSchemaReader.Read(session.Realm), session.FilePath);
                transaction.Commit();
            }

            return deleted;
        }

        /// <summary>较老的官方 schema 可能没有软删列；这时宁可拒删，也不要「报告已删但库里没变」。</summary>
        private static bool hasDeletePending(RealmSchemaSnapshot schema, string className)
        {
            if (schema.TryFindClass(className, out RealmClassSchema? classSchema) && classSchema.TryFindProperty("DeletePending", out _))
                return true;

            EzRealmSyncLog.Warn($"{className} 没有 DeletePending 列，无法软删。");
            return false;
        }

        private static string toClassName(RealmObjectClass objectClass) => objectClass switch
        {
            RealmObjectClass.BeatmapSet => OfficialBaselineSchema.BeatmapSet,
            RealmObjectClass.Score => OfficialBaselineSchema.Score,
            RealmObjectClass.BeatmapCollection => OfficialBaselineSchema.BeatmapCollection,
            _ => throw new InvalidOperationException($"类型 {objectClass} 没有对应的 Realm 表。"),
        };
    }
}
