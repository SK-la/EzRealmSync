using System.Diagnostics;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 检测 osu! / Ez2Lazer 是否正在运行（写入 Realm 前应关闭）。
    /// 也提供排他文件锁检测，防止并发写入冲突。
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
                    if (Process.GetProcessesByName(name).Length > 0)
                        return true;
                }
                catch
                {
                    // 忽略无权限等平台差异
                }
            }

            return false;
        }

        /// <summary>
        /// 多次重试检查游戏进程，每次间隔 500ms，降低竞态窗口。
        /// </summary>
        /// <param name="retryCount">重试次数，默认 3 次。</param>
        /// <returns>任意一次检测到游戏运行则返回 true。</returns>
        public static async Task<bool> RetryCheckAsync(int retryCount = 3)
        {
            for (int i = 0; i < retryCount; i++)
            {
                if (IsGameProcessRunning())
                    return true;

                if (i < retryCount - 1)
                    await Task.Delay(500).ConfigureAwait(false);
            }

            return false;
        }

        /// <summary>
        /// 尝试以排他写入模式打开 Realm 文件。若失败则说明文件被其他进程锁定（如游戏正在使用）。
        /// </summary>
        /// <param name="realmFilePath">Realm 文件的完整路径。</param>
        /// <returns>锁定该文件的进程名（若可识别），否则返回 null。</returns>
        public static string? TryAcquireExclusiveFileLock(string realmFilePath)
        {
            if (!File.Exists(realmFilePath))
                return null;

            try
            {
                using var stream = new FileStream(realmFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                // 成功获取锁，立即释放
                return null;
            }
            catch (IOException)
            {
                return $"无法以排他模式打开 Realm 文件（被其他进程锁定）：{realmFilePath}";
            }
            catch (UnauthorizedAccessException)
            {
                return $"无权限排他访问 Realm 文件：{realmFilePath}";
            }
        }

        public static string? TryGetBlockingProcessMessage()
        {
            if (!IsGameProcessRunning())
                return null;

            return "检测到 osu! / Ez2Lazer 正在运行。请先关闭游戏再写入或还原 Realm。";
        }

        /// <summary>
        /// 综合检查：先重试进程检测，再尝试排他文件锁。
        /// </summary>
        /// <param name="realmFilePath">可选的 Realm 文件路径；提供时将额外做排他锁检测。</param>
        /// <returns>错误消息；若全部通过则返回 null。</returns>
        public static async Task<string?> ComprehensiveCheckAsync(string? realmFilePath = null)
        {
            if (await RetryCheckAsync().ConfigureAwait(false))
                return "检测到 osu! / Ez2Lazer 正在运行。请先关闭游戏再写入或还原 Realm。";

            if (realmFilePath != null)
            {
                string? lockMessage = TryAcquireExclusiveFileLock(realmFilePath);
                if (lockMessage != null)
                    return lockMessage;
            }

            return null;
        }
    }
}
