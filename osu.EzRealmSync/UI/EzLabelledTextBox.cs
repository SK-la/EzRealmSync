using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;

namespace osu.EzRealmSync.UI
{
    public partial class EzLabelledTextBox : CompositeDrawable
    {
        private readonly Bindable<string> text = new Bindable<string>();

        public Bindable<string> Current
        {
            get => text;
            set => text.BindTo(value);
        }

        public EzLabelledTextBox(string label)
        {
            RelativeSizeAxes = Axes.X;
            Height = EzRealmSyncLayout.PATH_ROW_HEIGHT;

            BasicTextBox input;

            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, EzRealmSyncLayout.LABEL_WIDTH),
                    new Dimension(),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        new EzText
                        {
                            Text = label,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                        input = new BasicTextBox
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                    },
                },
            };

            input.Current.BindTarget = text;
        }
    }
}
