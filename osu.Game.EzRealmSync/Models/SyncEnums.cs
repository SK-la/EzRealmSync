using System.ComponentModel;

namespace osu.Game.EzRealmSync.Models
{
    public enum SyncDirection
    {
        EzToOfficial,
        OfficialToEz,
        /// <summary>Ez 扩展库 → Ez 扩展库（A→B，同类型）。</summary>
        EzToEz,
        /// <summary>ppy 客户端库 → ppy 客户端库（A→B，同类型）。</summary>
        PpyToPpy,
    }

    public enum EntityKind
    {
        BeatmapSet,
        Beatmap,
        Score,
        BeatmapCollection,
    }

    /// <summary>
    /// 左侧数据类型 Tab（单选，非多开关）。
    /// </summary>
    public enum EntityKindFilter
    {
        [Description("全部")]
        All,

        [Description("谱面集")]
        BeatmapSet,

        [Description("难度")]
        Beatmap,

        [Description("成绩")]
        Score,

        [Description("收藏夹")]
        BeatmapCollection,
    }

    public enum DiffCategory
    {
        SourceOnly,
        TargetOnly,
        Conflicted,
    }

    public enum MockDatasetSize
    {
        [Description("空")]
        Empty,

        [Description("中")]
        Medium,

        [Description("大")]
        Large,
    }

    public enum MockErrorInjection
    {
        [Description("无")]
        None,

        [Description("进程锁定")]
        ProcessLocked,

        [Description("无效路径")]
        InvalidPath,

        [Description("扫描取消")]
        ScanCancelled,
    }
}
