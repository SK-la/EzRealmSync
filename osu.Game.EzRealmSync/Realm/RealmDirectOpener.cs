#if HAS_EZ_OSU_GAME
using Realms;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 仅用 Realm SDK 创建/探测文件，不经过 <see cref="osu.Game.Database.RealmAccess"/> 构造（避免游戏启动维护与降级重建）。
    /// </summary>
    internal static class RealmDirectOpener
    {
        public static void CreateEmptyAtDiskSchema(string realmFilePath, int diskSchemaVersion)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(realmFilePath);

            string fullPath = Path.GetFullPath(realmFilePath);
            string? directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            deleteRealmSidecars(fullPath);

            var configuration = new RealmConfiguration(fullPath)
            {
                SchemaVersion = (ulong)diskSchemaVersion,
            };

            using (RealmInstance.GetInstance(configuration))
            {
            }

            deleteRealmSidecars(fullPath + ".lock");
        }

        private static void deleteRealmSidecars(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
#endif
