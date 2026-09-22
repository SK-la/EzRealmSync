using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace osu.Game.EzRealmSync.Runtime
{
    /// <summary>
    /// 解析产品进程的托管/原生运行时依赖（osu.Framework、Realm 与 Realm 原生 <c>realm-wrappers</c>）。
    /// <para>
    /// 产品进程不加载 <c>osu.Game.dll</c>（读写全走 DynamicRealm，官方产物由 OfficialWrite Worker 写出），
    /// 所以这里只预载框架与 Realm，不碰 osu.Game。
    /// </para>
    /// </summary>
    public static class EzRealmSyncRuntimeLibLoader
    {
        private static bool handlersRegistered;

        /// <summary>依赖所在目录；默认即进程目录（发布布局为平铺 + <c>runtimes/&lt;rid&gt;/native</c>）。</summary>
        public static string RuntimeLibDirectory { get; private set; } = AppContext.BaseDirectory;

        /// <param name="runtimeLibDirectoryOverride">仅测试使用：指定夹具目录，绕过进程目录。</param>
        public static void Install(string? runtimeLibDirectoryOverride = null)
        {
            if (!string.IsNullOrWhiteSpace(runtimeLibDirectoryOverride) && Directory.Exists(runtimeLibDirectoryOverride))
                RuntimeLibDirectory = Path.GetFullPath(runtimeLibDirectoryOverride);

            ensureHandlersRegistered();

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

            foreach (string directory in probeDirectories())
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

        private static string? resolveNativeLibraryPath(string libraryName)
        {
            string fileName = libraryName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? libraryName
                : libraryName + ".dll";

            string rid = resolveRuntimeIdentifier();

            foreach (string directory in probeDirectories())
            {
                foreach (string candidate in new[] { Path.Combine(directory, "runtimes", rid, "native", fileName), Path.Combine(directory, fileName) })
                {
                    if (File.Exists(candidate))
                        return candidate;
                }
            }

            return null;
        }

        private static IEnumerable<string> probeDirectories()
        {
            yield return RuntimeLibDirectory;

            if (!string.Equals(RuntimeLibDirectory, AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase))
                yield return AppContext.BaseDirectory;
        }

        private static void verifyRealmNativeLibraryPresent()
        {
            if (resolveNativeLibraryPath("realm-wrappers") != null)
                return;

            string rid = resolveRuntimeIdentifier();
            string hint = Path.Combine(RuntimeLibDirectory, "runtimes", rid, "native", "realm-wrappers.dll");

            throw new InvalidOperationException(
                $"未找到 Realm 原生库 realm-wrappers.dll（预期路径：{hint}）。请重新安装/构建 EzRealmSync，或检查发布目录的 runtimes/{rid}/native 是否完整。");
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
