namespace osu.Game.EzRealmSync.Models
{
    /// <summary>
    /// 与 osu!lazer <c>IFileInfo.GetStoragePath()</c> 一致的 files/ 相对路径。
    /// </summary>
    public static class RealmFilePathHelper
    {
        public static string GetStoragePath(string hash) =>
            Path.Combine(hash.Remove(1), hash.Remove(2), hash);

        public static string GetFullPath(string filesDirectory, string hash) =>
            Path.Combine(filesDirectory, GetStoragePath(hash));
    }
}
