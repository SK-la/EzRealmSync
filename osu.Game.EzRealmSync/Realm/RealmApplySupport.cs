using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// Apply 能力声明（可单测，不依赖 Realm）。
    /// </summary>
    public static class RealmApplySupport
    {
        public static bool SupportsDirection(SyncDirection direction) =>
            direction is SyncDirection.EzToOfficial or SyncDirection.OfficialToEz;

        public static string? ValidateApplyRequest(ApplyRequest request)
        {
            if (!SupportsDirection(request.Direction))
                return "不支持的同步方向。";

            if (request.ItemIds.Count == 0)
                return "未选择任何要处理的项。";

            if (string.IsNullOrWhiteSpace(request.Paths.EzRealmFile) || string.IsNullOrWhiteSpace(request.Paths.OfficialRealmFile))
                return "请先配置 Ez 与官方 client.realm 路径。";

            return null;
        }
    }
}
