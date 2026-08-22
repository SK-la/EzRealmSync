using System.Reflection;
using NUnit.Framework;
using osu.Game.EzRealmSync.Runtime;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class EzRealmSyncRuntimeLibLoaderTest
    {
        [Test]
        public void InstallSidecarHost_does_not_preload_osu_game()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "EzRealmSyncLoader", Guid.NewGuid().ToString("N"));
            string libDir = Path.Combine(tempRoot, "lib");
            Directory.CreateDirectory(libDir);
            File.WriteAllText(Path.Combine(libDir, "osu.Game.dll"), string.Empty);
            File.WriteAllText(Path.Combine(libDir, "osu.Framework.dll"), string.Empty);
            File.WriteAllText(Path.Combine(libDir, "Realm.dll"), string.Empty);

            string ridDir = Path.Combine(libDir, "runtimes", "win-x64", "native");
            Directory.CreateDirectory(ridDir);
            File.WriteAllText(Path.Combine(ridDir, "realm-wrappers.dll"), string.Empty);

            try
            {
                var preloadedGame = findLoadedAssembly("osu.Game");
                EzRealmSyncRuntimeLibLoader.InstallSidecarHost(libDir);

                if (preloadedGame == null)
                    Assert.That(findLoadedAssembly("osu.Game"), Is.Null, "Sidecar host 不应 preload osu.Game");

                Assert.That(EzRealmSyncRuntimeLibLoader.RuntimeLibDirectory, Is.EqualTo(libDir));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
        }

        private static Assembly? findLoadedAssembly(string simpleName) =>
            AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase));
    }
}
