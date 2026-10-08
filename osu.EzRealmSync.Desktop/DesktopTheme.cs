using System.Windows.Interop;
using System.Windows.Media;

namespace osu.EzRealmSync.Desktop
{
    /// <summary>
    /// Applies the saved light/dark theme and a solid window fill when Mica is unavailable.
    /// Windows 10 ignores Mica; a transparent window there stays white while Ez text stays light.
    /// </summary>
    public static class DesktopTheme
    {
        public static bool IsDark { get; private set; } = true;

        public static void Apply(bool dark)
        {
            IsDark = dark;
            var theme = dark ? ApplicationTheme.Dark : ApplicationTheme.Light;
            var backdrop = preferredBackdrop();

            ApplicationThemeManager.Apply(theme, backdrop, updateAccent: true);
            applyPalette(dark);

            if (Application.Current == null)
                return;

            foreach (Window window in Application.Current.Windows)
                applyChrome(window);
        }

        public static void Attach(Window window)
        {
            window.SourceInitialized += (_, _) => applyChrome(window);

            if (PresentationSource.FromVisual(window) != null)
                applyChrome(window);
        }

        private static WindowBackdropType preferredBackdrop() =>
            WindowBackdrop.IsSupported(WindowBackdropType.Mica)
                ? WindowBackdropType.Mica
                : WindowBackdropType.None;

        private static void applyChrome(Window window)
        {
            var theme = IsDark ? ApplicationTheme.Dark : ApplicationTheme.Light;
            var backdrop = preferredBackdrop();

            if (window is FluentWindow fluent && fluent.WindowBackdropType != backdrop)
                fluent.WindowBackdropType = backdrop;

            WindowBackgroundManager.UpdateBackground(window, theme, backdrop);

            if (backdrop != WindowBackdropType.None)
                return;

            // Mica is unsupported: keep an opaque fill so light Ez text is not drawn on white.
            if (Application.Current?.TryFindResource("ApplicationBackgroundBrush") is not Brush brush)
                return;

            window.Background = brush;

            if (brush is SolidColorBrush solid && PresentationSource.FromVisual(window) is HwndSource { CompositionTarget: { } target })
                target.BackgroundColor = solid.Color;
        }

        private static void applyPalette(bool dark)
        {
            if (dark)
            {
                setColor("EzBrushBgCard", 0x88, 0x22, 0x22, 0x33);
                setColor("EzBrushBgSettingsFlyout", 0xF0, 0x1E, 0x1E, 0x28);
                setColor("EzBrushFgPrimary", 0xFF, 0xE4, 0xE4, 0xEA);
                setColor("EzBrushFgMuted", 0xFF, 0xB4, 0xB4, 0xBE);
                setColor("EzBrushBorder", 0xFF, 0x3A, 0x3A, 0x4A);
                return;
            }

            setColor("EzBrushBgCard", 0xE6, 0xFF, 0xFF, 0xFF);
            setColor("EzBrushBgSettingsFlyout", 0xFF, 0xF6, 0xF6, 0xF8);
            setColor("EzBrushFgPrimary", 0xFF, 0x1B, 0x1B, 0x1F);
            setColor("EzBrushFgMuted", 0xFF, 0x5A, 0x5A, 0x66);
            setColor("EzBrushBorder", 0xFF, 0xD6, 0xD6, 0xDE);
        }

        private static void setColor(string key, byte a, byte r, byte g, byte b)
        {
            if (Application.Current?.TryFindResource(key) is SolidColorBrush brush)
                brush.Color = Color.FromArgb(a, r, g, b);
        }
    }
}
