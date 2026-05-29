using System.ComponentModel;

namespace osu.Game.EzRealmSync.Models
{
    public enum SyncDirection
    {
        EzToOfficial,
        OfficialToEz,
    }

    public enum EntityKind
    {
        BeatmapSet,
        Beatmap,
        Score,
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
