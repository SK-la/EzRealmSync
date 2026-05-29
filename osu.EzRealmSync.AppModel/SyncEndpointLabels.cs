using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.AppModel
{
    public static class SyncEndpointLabels
    {
        /// <summary>Desktop 数据/同步页：A 恒为源、B 恒为目标（与内部 <see cref="SyncDirection"/> 无关）。</summary>
        public static void Get(SyncDirection direction, out string source, out string target)
        {
            source = "A";
            target = "B";
        }
    }
}
