using osu.Framework.Graphics.Sprites;
using osuTK.Graphics;

namespace osu.EzRealmSync.UI
{
    internal static class EzTheme
    {
        public static Color4 Background => new Color4(25, 25, 35, 255);
        public static Color4 Panel => new Color4(35, 35, 48, 255);
        public static Color4 PanelDark => new Color4(28, 28, 38, 255);
        public static Color4 Accent => new Color4(255, 102, 171, 255);
        public static Color4 AccentSecondary => new Color4(121, 163, 255, 255);
        public static Color4 Text => Color4.White;
        public static Color4 TextMuted => new Color4(180, 180, 190, 255);

        public static FontUsage Font(float size = 14, string? weight = null)
        {
            // Framework 内置 Roboto 无 SemiBold，映射到 Bold 避免错字重。
            weight = weight switch
            {
                "SemiBold" => "Bold",
                _ => weight,
            };

            return FontUsage.Default.With(size: size, weight: weight);
        }
    }
}
