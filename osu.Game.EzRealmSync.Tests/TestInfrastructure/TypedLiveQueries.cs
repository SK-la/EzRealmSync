using osu.Game.Beatmaps;
using osu.Game.Scoring;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Tests.TestInfrastructure
{
    /// <summary>
    /// 产品侧的 <c>Live*</c> 查询已随 typed 读路径下线；这份只给 typed 对照测试复刻同一套过滤口径
    /// （Realm LINQ 翻不动 <c>DeletePending</c> 之类谓词，一律在内存里过滤）。
    /// </summary>
    internal static class TypedLiveQueries
    {
        public static IEnumerable<BeatmapInfo> LiveBeatmaps(this RealmInstance realm) =>
            realm.All<BeatmapInfo>().AsEnumerable().Where(b => !b.Hidden && (b.BeatmapSet == null || !b.BeatmapSet.DeletePending));

        public static IEnumerable<ScoreInfo> LiveScores(this RealmInstance realm) =>
            realm.All<ScoreInfo>().AsEnumerable().Where(s => !s.DeletePending);
    }
}
