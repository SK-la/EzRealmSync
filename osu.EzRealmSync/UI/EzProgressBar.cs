using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;

namespace osu.EzRealmSync.UI
{
    public partial class EzProgressBar : CompositeDrawable
    {
        private readonly Box fill = null!;
        private double progress;

        public double Progress
        {
            get => progress;
            set
            {
                progress = Math.Clamp(value, 0, 1);
                if (fill.IsLoaded)
                    fill.Width = (float)(DrawWidth * progress);
            }
        }

        public EzProgressBar()
        {
            RelativeSizeAxes = Axes.X;
            Height = 8;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = EzTheme.PanelDark,
                },
                fill = new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Height = 1,
                    Colour = EzTheme.Accent,
                },
            };
        }

        protected override void Update()
        {
            base.Update();
            fill.Width = (float)(DrawWidth * progress);
        }
    }
}
