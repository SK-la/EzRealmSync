using System.ComponentModel;

namespace osu.Game.EzRealmSync.Models
{
    public enum RealmSetOperation
    {
        [Description("交集")]
        Intersection,

        [Description("并集")]
        Union,

        [Description("差集 (A−B)")]
        Difference,

        [Description("对称差")]
        SymmetricDifference,
    }

    public enum RealmSyncAction
    {
        [Description("添加到目标")]
        Add,

        [Description("从源删除")]
        Delete,
    }
}
