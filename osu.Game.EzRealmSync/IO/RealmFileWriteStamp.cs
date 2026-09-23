namespace osu.Game.EzRealmSync.IO
{
    /// <summary>
    /// 成功提交后的收尾：把目标 Realm 的修改时间置为当前时间，并留一条可追溯的 INFO。
    ///
    /// 为什么需要它：Realm 通过 mmap 写页，Windows 不会因为映射写入而更新 <c>LastWriteTime</c>，
    /// 提交又常常不扩展文件长度（改写的是文件里已分配的页），于是"同步成功但文件日期与大小一动不动，
    /// 只有 .lock 变了"——按资源管理器判断的用户会以为根本没写进去（实际数据已落盘，换进程重开可见）。
    /// 内容确实变了，时间戳就该跟着变，否则文件系统的信号在骗人。
    ///
    /// 提交后失败（找不到文件、无权限）只记 WARN：调用点的写入已经成功，这里不该把成功变成报错。
    /// </summary>
    public static class RealmFileWriteStamp
    {
        public static void MarkWritten(string realmFilePath)
        {
            if (string.IsNullOrWhiteSpace(realmFilePath))
                return;

            try
            {
                string fullPath = Path.GetFullPath(realmFilePath);

                if (!File.Exists(fullPath))
                    return;

                File.SetLastWriteTimeUtc(fullPath, DateTime.UtcNow);
                EzRealmSyncLog.Info($"Realm 已写入：{fullPath}");
            }
            catch (Exception ex)
            {
                EzRealmSyncLog.Warn($"更新 Realm 文件时间戳失败：{realmFilePath}（{ex.GetType().Name}: {ex.Message}）");
            }
        }
    }
}
