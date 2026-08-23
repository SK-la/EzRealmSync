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
        [Description("添加")]
        Add,

        [Description("删除")]
        Delete,
    }

    /// <summary>同步页执行写入/删除的操作目标端（与对比用的 A/B 下拉独立）。</summary>
    public enum SyncWriteEndpoint
    {
        [Description("A")]
        A,

        [Description("B")]
        B,
    }
}
