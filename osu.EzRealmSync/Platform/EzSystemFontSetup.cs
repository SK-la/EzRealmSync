using osu.Framework.Graphics.Rendering;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;

namespace osu.EzRealmSync.Platform
{
    internal static class EzSystemFontSetup
    {
        public static string? LoadedFamily { get; private set; }

        public static void Load(
            IRenderer renderer,
            FontStore fonts,
            Action<ResourceStore<byte[]>, string, FontStore> addFont)
        {
            if (shouldUseNotoFallback())
            {
                EzCjkFontLoader.Load(renderer, fonts, addFont);

                if (EzCjkFontLoader.Loaded)
                {
                    LoadedFamily = "Noto (EZREALMSYNC_USE_NOTO_FONTS)";
                    return;
                }
            }

            var store = new SystemFontGlyphLookupStore(renderer);
            fonts.AddStore(store);
            LoadedFamily = store.FamilyName;
            Logger.Log($"已启用系统字体回退：{LoadedFamily}", LoggingTarget.Runtime, LogLevel.Important);
        }

        private static bool shouldUseNotoFallback()
        {
            string? value = Environment.GetEnvironmentVariable("EZREALMSYNC_USE_NOTO_FONTS");
            return string.Equals(value, "1", StringComparison.Ordinal) || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
