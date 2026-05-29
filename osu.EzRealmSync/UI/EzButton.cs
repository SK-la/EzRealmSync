using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osuTK.Graphics;

namespace osu.EzRealmSync.UI
{
    public partial class EzButton : BasicButton
    {
        public EzButton()
        {
            BackgroundColour = EzTheme.AccentSecondary;
            HoverColour = EzTheme.Accent;
            Height = 32;
            Padding = new MarginPadding { Horizontal = 12, Vertical = 6 };
        }

        /// <summary>按文字宽度自适应（用于侧栏等）。</summary>
        public void SizeToText()
        {
            AutoSizeAxes = Axes.X;
        }

        /// <summary>填满 Grid 单元格。</summary>
        public void FillCell()
        {
            AutoSizeAxes = Axes.None;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            SpriteText.Colour = Color4.White;
            SpriteText.Font = EzTheme.Font(14, "SemiBold");
        }
    }
}
