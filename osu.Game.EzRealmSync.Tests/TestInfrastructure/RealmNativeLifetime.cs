using NUnit.Framework;
using Realms;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Tests.TestInfrastructure
{
    /// <summary>
    /// Realm-core 在进程退出时若仍有未刷完的 SharedRealm / 写事务最终化，可能触发
    /// <c>!realm.is_in_transaction()</c> 把 test host 打崩。测试侧统一创建与释放。
    /// </summary>
    public static class RealmNativeLifetime
    {
        public static void CreateEmptyRealmFile(string path, ulong schemaVersion)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var config = new RealmConfiguration(path) { SchemaVersion = schemaVersion };
            using (RealmInstance.GetInstance(config))
            {
            }

            Flush();
        }

        public static void DeleteRealmFiles(string path)
        {
            Flush();

            tryDelete(path);
            tryDelete(path + ".lock");
            tryDelete(path + ".note");
            tryDelete(path + ".management");
        }

        public static void Flush()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private static void tryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // 锁尚未释放时忽略；Flush 后重试由调用方决定。
            }
        }
    }

    [SetUpFixture]
    public sealed class RealmNativeLifetimeFixture
    {
        [OneTimeTearDown]
        public void FlushNativeHandles() => RealmNativeLifetime.Flush();
    }
}
