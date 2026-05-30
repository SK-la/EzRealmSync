using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace osu.Game.EzRealmSync.Runtime
{
    /// <summary>
    /// 从 exe 同目录下的 <c>lib/</c>（或兼容的旧版平铺布局）解析 Ez osu.Game 运行时依赖。
    /// 须在首次使用 Realm / osu.Game 类型之前调用 <see cref="Install"/>。
    /// </summary>
    public static class EzRealmSyncRuntimeLibLoader
    {
        private static bool installed;
        private static string? runtimeLibDirectory;

        public static string? RuntimeLibDirectory => runtimeLibDirectory;

        public static void Install()
        {
            if (installed)
                return;

            installed = true;
            runtimeLibDirectory = EzRealmSyncBackend.ResolveRuntimeLibDirectory();

            if (runtimeLibDirectory == null)
                return;

            foreach (string name in preloadOrder)
                tryLoadManaged(name);

            verifyRealmNativeLibraryPresent();

            AssemblyLoadContext.Default.Resolving += onResolving;
            AssemblyLoadContext.Default.ResolvingUnmanagedDll += onResolvingUnmanagedDll;
        }

        private static readonly string[] preloadOrder =
        {
            "osu.Framework",
            "osu.Game",
            "Realm",
        };

        private static Assembly? onResolving(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName.Name))
                return null;

            return tryLoadManaged(assemblyName.Name);
        }

        private static IntPtr onResolvingUnmanagedDll(Assembly assembly, string libraryName)
        {
            string? path = resolveNativeLibraryPath(libraryName);
            return path != null ? NativeLibrary.Load(path) : IntPtr.Zero;
        }

        private static Assembly? tryLoadManaged(string assemblyName)
        {
            string fileName = assemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? assemblyName
                : assemblyName + ".dll";

            foreach (string directory in probeManagedDirectories())
            {
                string path = Path.Combine(directory, fileName);
                if (!File.Exists(path))
                    continue;

                try
                {
                    return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
                }
                catch (FileLoadException)
                {
                    // 已加载其它版本时忽略，交给默认解析。
                }
                catch (BadImageFormatException)
                {
                    // 架构不匹配等，尝试下一个目录。
                }
            }

            return null;
        }

        private static IEnumerable<string> probeManagedDirectories()
        {
            if (runtimeLibDirectory != null)
                yield return runtimeLibDirectory;

            yield return AppContext.BaseDirectory;
        }

        private static string? resolveNativeLibraryPath(string libraryName)
        {
            string fileName = libraryName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? libraryName
                : libraryName + ".dll";

            foreach (string directory in probeNativeDirectories())
            {
                string path = Path.Combine(directory, fileName);
                if (File.Exists(path))
                    return path;
            }

            return null;
        }

        private static IEnumerable<string> probeNativeDirectories()
        {
            string rid = resolveRuntimeIdentifier();

            if (runtimeLibDirectory != null)
            {
                yield return Path.Combine(runtimeLibDirectory, "runtimes", rid, "native");
                yield return runtimeLibDirectory;
            }

            yield return Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native");
            yield return AppContext.BaseDirectory;
        }

        private static void verifyRealmNativeLibraryPresent()
        {
            if (resolveNativeLibraryPath("realm-wrappers") != null)
                return;

            string hint = runtimeLibDirectory != null
                ? Path.Combine(runtimeLibDirectory, "runtimes", resolveRuntimeIdentifier(), "native", "realm-wrappers.dll")
                : "exe/lib/runtimes/.../realm-wrappers.dll";

            throw new InvalidOperationException(
                $"未找到 Realm 原生库 realm-wrappers.dll（预期路径：{hint}）。请执行：dotnet build -t:SyncEzRealmLibs EzRealmSync.sln -c Debug，并重新生成 Desktop。");
        }

        private static string resolveRuntimeIdentifier()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Environment.Is64BitProcess ? "win-x64" : "win-x86";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.Arm64 => "osx-arm64",
                    _ => "osx-x64",
                };

            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "linux-arm64",
                _ => "linux-x64",
            };
        }
    }
}
