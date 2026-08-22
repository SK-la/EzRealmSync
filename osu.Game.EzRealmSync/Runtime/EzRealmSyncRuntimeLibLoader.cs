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
        private static bool handlersRegistered;
        private static string? runtimeLibDirectory;
        private static readonly List<string> prependProbeDirectories = new List<string>();

        public static string? RuntimeLibDirectory => runtimeLibDirectory;

        public static void Install(string? runtimeLibDirectoryOverride = null)
        {
            if (!string.IsNullOrWhiteSpace(runtimeLibDirectoryOverride) && Directory.Exists(runtimeLibDirectoryOverride))
                runtimeLibDirectory = Path.GetFullPath(runtimeLibDirectoryOverride);
            else if (runtimeLibDirectory == null)
                runtimeLibDirectory = EzRealmSyncBackend.ResolveRuntimeLibDirectory();

            ensureHandlersRegistered();

            if (runtimeLibDirectory == null)
                return;

            foreach (string name in preloadOrder)
                tryLoadManaged(name);

            verifyRealmNativeLibraryPresent();
        }

        /// <summary>将目录置于 probe 链最前（reader Sidecar job 内 reader lib 优先于主 lib）。</summary>
        public static void PrependProbeDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return;

            string full = Path.GetFullPath(directory);
            prependProbeDirectories.RemoveAll(d => string.Equals(d, full, StringComparison.OrdinalIgnoreCase));
            prependProbeDirectories.Insert(0, full);
        }

        private static readonly string[] preloadOrder =
        {
            "osu.Framework",
            "osu.Game",
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
            foreach (string directory in prependProbeDirectories)
                yield return directory;

            if (runtimeLibDirectory != null)
                yield return runtimeLibDirectory;

            yield return AppContext.BaseDirectory;

            string parentLib = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "lib"));
            if (Directory.Exists(parentLib))
                yield return parentLib;
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

            foreach (string directory in prependProbeDirectories)
            {
                yield return Path.Combine(directory, "runtimes", rid, "native");
                yield return directory;
            }

            if (runtimeLibDirectory != null)
            {
                yield return Path.Combine(runtimeLibDirectory, "runtimes", rid, "native");
                yield return runtimeLibDirectory;
            }

            yield return Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native");
            yield return AppContext.BaseDirectory;

            string parentLib = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "lib"));
            if (Directory.Exists(parentLib))
            {
                yield return Path.Combine(parentLib, "runtimes", rid, "native");
                yield return parentLib;
            }
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
