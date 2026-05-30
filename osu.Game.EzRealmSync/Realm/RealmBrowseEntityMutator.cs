#if HAS_EZ_OSU_GAME
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;
using osu.Game.Scoring;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 数据 Tab：对谱面集 / 成绩 / 收藏夹的 Realm 写操作（不触发 schema 迁移）。
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
            RealmObjectClass.BeatmapCollection => true,
            _ => false,
        };

        public static int Delete(RealmAccess access, RealmObjectClass objectClass, IReadOnlyList<Guid> ids)
        {
            if (!SupportsMutation(objectClass))
                throw new InvalidOperationException($"类型 {objectClass} 不支持从数据页删除。");

            int deleted = 0;

            access.Write(realm =>
            {
                foreach (var id in ids)
                {
                    if (tryDeleteOne(realm, objectClass, id))
                        deleted++;
                }
            });

            return deleted;
        }

        private static bool tryDeleteOne(RealmInstance realm, RealmObjectClass objectClass, Guid id)
        {
            switch (objectClass)
            {
                case RealmObjectClass.BeatmapSet:
                    if (realm.Find<BeatmapSetInfo>(id) is BeatmapSetInfo set)
                    {
                        set.DeletePending = true;
                        return true;
                    }

                    return false;

                case RealmObjectClass.Score:
                    if (realm.Find<ScoreInfo>(id) is ScoreInfo score)
                    {
                        score.DeletePending = true;
                        return true;
                    }

                    return false;

                case RealmObjectClass.BeatmapCollection:
                    if (realm.Find<BeatmapCollection>(id) is BeatmapCollection collection)
                    {
                        realm.Remove(collection);
                        return true;
                    }

                    return false;

                default:
                    return false;
            }
        }
    }
}
#endif
