namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 检测 osu! / Ez2Lazer 是否正在运行（写入 Realm 前应关闭）。
    /// </summary>
    public static class RealmProcessGuard
    {
        private static readonly string[] process_names = { "osu!", "osu", "Ez2Lazer" };

        public static bool IsGameProcessRunning()
        {
            foreach (string name in process_names)
            {
                try
                {
                    if (System.Diagnostics.Process.GetProcessesByName(name).Length > 0)
                        return true;
                }
                catch
                {
                    // 忽略无权限等平台差异
                }
            }

            return false;
        }

        public static string? TryGetBlockingProcessMessage()
        {
            if (!IsGameProcessRunning())
                return null;

            return "检测到 osu! / Ez2Lazer 正在运行。请先关闭游戏再写入或还原 Realm。";
        }
    }
}
