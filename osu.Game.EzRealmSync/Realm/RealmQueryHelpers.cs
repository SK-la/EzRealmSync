#if HAS_EZ_OSU_GAME
using osu.Game.Beatmaps;
using osu.Game.Scoring;
using osu.Game.Skinning;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// Realm LINQ 对部分谓词（如 <see cref="BeatmapSetInfo.DeletePending"/>）翻译会失败，须在内存中过滤。
    /// </summary>
    internal static class RealmQueryHelpers
    {
        public static IEnumerable<BeatmapSetInfo> LiveBeatmapSets(this RealmInstance realm) => realm.All<BeatmapSetInfo>().AsEnumerable().Where(s => !s.DeletePending);

        public static IEnumerable<BeatmapInfo> LiveBeatmaps(this RealmInstance realm) => realm.All<BeatmapInfo>().AsEnumerable().Where(b => b.BeatmapSet == null || !b.BeatmapSet.DeletePending);

        public static IEnumerable<ScoreInfo> LiveScores(this RealmInstance realm) => realm.All<ScoreInfo>().AsEnumerable().Where(s => !s.DeletePending);

        public static IEnumerable<SkinInfo> LiveSkins(this RealmInstance realm) => realm.All<SkinInfo>().AsEnumerable().Where(s => !s.DeletePending);
    }
}
#endif
