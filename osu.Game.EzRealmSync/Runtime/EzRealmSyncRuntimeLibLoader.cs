using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace osu.Game.EzRealmSync.Runtime
{
    /// <summary>
    /// 解析产品进程的托管/原生运行时依赖（osu.Framework、Realm 与 Realm 原生 realm-wrappers）。
    /// <para>
    /// 产品进程不加载 <c>osu.Game.dll</c>：Realm 读写全走 DynamicRealm，官方产物的写出交给
    /// OfficialWrite Worker（见 docs/DATA-OPERATIONS.zh.md）。
    /// </para>
    /// </summary>
    public static class EzRealmSyncRuntimeLibLoader
    {
        private static bool handlersRegistered;

        public static string? RuntimeLibDirectory { get; private set; }

        public static void Install(string? runtimeLibDirectoryOverride = null)
        {
            if (!string.IsNullOrWhiteSpace(runtimeLibDirectoryOverride) && Directory.Exists(runtimeLibDirectoryOverride))
                RuntimeLibDirectory = Path.GetFullPath(runtimeLibDirectoryOverride);
            else if (RuntimeLibDirectory == null)
                RuntimeLibDirectory = EzRealmSyncBackend.ResolveRuntimeLibDirectory();

            ensureHandlersRegistered();

            if (RuntimeLibDirectory == null)
                return;

            foreach (string name in preloadOrder)
                tryLoadManaged(name);

            verifyRealmNativeLibraryPresent();
        }

        private static readonly string[] preloadOrder =
        {
            "osu.Framework",
            "Realm",
        };

        private static void ensureHandlersRegistered()
        {
            if (handlersRegistered)
                return;

            handlersRegistered = true;
            AssemblyLoadContext.Default.Resolving += onResolving;
            AssemblyLoadContext.Default.ResolvingUnmanagedDll += onResolvingUnmanagedDll;
        }

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
            if (RuntimeLibDirectory != null)
                yield return RuntimeLibDirectory;

            yield return AppContext.BaseDirectory;

            string parentHost = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
            if (File.Exists(Path.Combine(parentHost, "EzRealmSync.exe")))
                yield return parentHost;
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

            if (RuntimeLibDirectory != null)
            {
                yield return Path.Combine(RuntimeLibDirectory, "runtimes", rid, "native");
                yield return RuntimeLibDirectory;
            }

            yield return Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native");
            yield return AppContext.BaseDirectory;

            string parentHost = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
            if (File.Exists(Path.Combine(parentHost, "EzRealmSync.exe")))
            {
                yield return Path.Combine(parentHost, "runtimes", rid, "native");
                yield return parentHost;
            }
        }

        private static void verifyRealmNativeLibraryPresent()
        {
            if (resolveNativeLibraryPath("realm-wrappers") != null)
                return;

            string hint = RuntimeLibDirectory != null
                ? Path.Combine(RuntimeLibDirectory, "runtimes", resolveRuntimeIdentifier(), "native", "realm-wrappers.dll")
                : "exe/runtimes/.../realm-wrappers.dll";

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
