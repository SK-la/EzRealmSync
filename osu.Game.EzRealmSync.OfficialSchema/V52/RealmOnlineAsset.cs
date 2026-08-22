using osu.Game.EzRealmSync.OfficialSchema.V51;
using Realms;

namespace osu.Game.EzRealmSync.OfficialSchema.V52
{
    /// <summary>ppy upstream 52：在线资源缓存表（无 Ez 列）。与 osu.Game.Models.RealmOnlineAsset 对齐。</summary>
    public class RealmOnlineAsset : RealmObject
    {
        public RealmNamedFileUsage File { get; set; } = null!;

        [Indexed]
        public DateTimeOffset LastAccessed { get; set; } = DateTimeOffset.Now;
    }
}
