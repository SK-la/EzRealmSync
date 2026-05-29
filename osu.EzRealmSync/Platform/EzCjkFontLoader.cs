using System.Reflection;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.EzRealmSync.Platform
{
    /// <summary>
    /// 为纯 Framework 宿主注册 Noto CJK 字体（Roboto 不含中文，缺字会显示为 ?）。
    /// </summary>
    internal static class EzCjkFontLoader
    {
        private static readonly string[] font_assets =
        {
            @"Noto/Noto-CJK-Basic",
            @"Noto/Noto-CJK-Compatibility",
            @"Noto/Noto-Basic",
        };

        public static bool Loaded { get; private set; }

        public static void Load(
            IRenderer renderer,
            FontStore fonts,
            Action<ResourceStore<byte[]>, string, FontStore> addFont)
        {
            if (tryLoadFromResourcesAssembly(renderer, fonts, addFont))
            {
                Loaded = true;
                return;
            }

            foreach (string root in enumerateFontRoots())
            {
                if (!Directory.Exists(Path.Combine(root, "Noto")))
                    continue;

                if (tryLoadFromDirectory(renderer, fonts, addFont, root))
                {
                    Logger.Log($"已加载 CJK 字体：{root}", LoggingTarget.Runtime, LogLevel.Important);
                    Loaded = true;
                    return;
                }
            }

            Logger.Log(
                "未找到 Noto CJK 字体，中文可能显示为 ?。请将 osu-resources 的 Fonts/Noto 复制到 Assets/Fonts，"
                + "或设置环境变量 EZREALMSYNC_FONTS，或放置 osu.Game.Resources.dll。详见 Assets/Fonts/README.md",
                LoggingTarget.Runtime,
                LogLevel.Important);
        }

        private static bool tryLoadFromResourcesAssembly(
            IRenderer renderer,
            FontStore fonts,
            Action<ResourceStore<byte[]>, string, FontStore> addFont)
        {
            foreach (string dllName in new[] { "osu.Game.Resources.dll", "ez2lazer.Game.Resources.dll" })
            {
                string path = Path.Combine(AppContext.BaseDirectory, dllName);
                if (!File.Exists(path))
                    continue;

                try
                {
                    registerFonts(renderer, fonts, addFont, new NamespacedResourceStore<byte[]>(new DllResourceStore(path), @"Fonts"));
                    Logger.Log($"已加载 CJK 字体：{dllName}", LoggingTarget.Runtime, LogLevel.Important);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Log($"加载 {dllName} 字体失败：{ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                }
            }

            foreach (string assemblyName in new[] { "osu.Game.Resources", "ez2lazer.Game.Resources" })
            {
                try
                {
                    var assembly = Assembly.Load(assemblyName);
                    registerFonts(renderer, fonts, addFont, new NamespacedResourceStore<byte[]>(new DllResourceStore(assembly), @"Fonts"));
                    Logger.Log($"已加载 CJK 字体：{assemblyName}", LoggingTarget.Runtime, LogLevel.Important);
                    return true;
                }
                catch
                {
                    // 未引用资源程序集时忽略
                }
            }

            return false;
        }

        private static bool tryLoadFromDirectory(
            IRenderer renderer,
            FontStore fonts,
            Action<ResourceStore<byte[]>, string, FontStore> addFont,
            string fontRoot)
        {
            try
            {
                var storage = new NativeStorage(fontRoot);
                registerFonts(renderer, fonts, addFont, new StorageBackedResourceStore(storage));
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"加载字体目录失败 ({fontRoot})：{ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                return false;
            }
        }

        private static void registerFonts(
            IRenderer renderer,
            FontStore fonts,
            Action<ResourceStore<byte[]>, string, FontStore> addFont,
            IResourceStore<byte[]> source)
        {
            var byteStore = new ResourceStore<byte[]>();
            byteStore.AddStore(source);

            var cjkFontStore = new FontStore(renderer, scaleAdjust: 100, minFilterMode: TextureFilteringMode.Linear);
            fonts.AddStore(cjkFontStore);

            foreach (string asset in font_assets)
                addFont(byteStore, asset, cjkFontStore);
        }

        private static IEnumerable<string> enumerateFontRoots()
        {
            yield return Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts");

            string? env = Environment.GetEnvironmentVariable("EZREALMSYNC_FONTS");
            if (!string.IsNullOrWhiteSpace(env))
                yield return env;

            string? dir = AppContext.BaseDirectory;

            for (int i = 0; i < 8 && dir != null; i++)
            {
                foreach (string relative in new[]
                {
                    Path.Combine("osu-resources", "osu.Game.Resources", "Fonts"),
                    Path.Combine("..", "osu-resources", "osu.Game.Resources", "Fonts"),
                    Path.Combine("..", "..", "osu-resources", "osu.Game.Resources", "Fonts"),
                })
                {
                    string candidate = Path.GetFullPath(Path.Combine(dir, relative));
                    if (Directory.Exists(Path.Combine(candidate, "Noto")))
                        yield return candidate;
                }

                dir = Directory.GetParent(dir)?.FullName;
            }
        }
    }
}
