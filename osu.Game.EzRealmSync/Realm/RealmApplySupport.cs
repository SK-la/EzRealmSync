using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// Apply 能力声明（可单测，不依赖 Realm）。
    /// </summary>
    public static class RealmApplySupport
    {
        public static bool SupportsDirection(SyncDirection direction) => direction switch
        {
            SyncDirection.EzToOfficial => true,
            SyncDirection.OfficialToEz => true,
            SyncDirection.EzToEz => true,
            SyncDirection.PpyToPpy => true,
            _ => false,
        };

        public static string? ValidateApplyRequest(ApplyRequest request)
        {
            if (request.WritePlan != null)
                return validatePaths(request.WritePlan.SourceRealmFilePath, request.WritePlan.TargetRealmFilePath, request.ItemIds.Count);

            if (!SupportsDirection(request.Direction))
                return "不支持的同步方向。";

            if (!string.IsNullOrWhiteSpace(request.Paths.SourceRealmFilePath)
                && !string.IsNullOrWhiteSpace(request.Paths.TargetRealmFilePath))
            {
                return validatePaths(request.Paths.SourceRealmFilePath, request.Paths.TargetRealmFilePath, request.ItemIds.Count);
            }

            if (string.IsNullOrWhiteSpace(request.Paths.EzRealmFile) || string.IsNullOrWhiteSpace(request.Paths.OfficialRealmFile))
                return "请先配置源与目标 client.realm 路径。";

            return validateItemCount(request.ItemIds.Count);
        }

        private static string? validatePaths(string sourcePath, string targetPath, int itemCount)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(targetPath))
                return "请先选择源库与目标库（A→B）。";

            return validateItemCount(itemCount);
        }

        private static string? validateItemCount(int itemCount)
        {
            if (itemCount == 0)
                return "未选择任何要处理的项。";

            return null;
        }
    }
}
