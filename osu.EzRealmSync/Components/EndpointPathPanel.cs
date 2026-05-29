using osu.EzRealmSync.UI;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace osu.EzRealmSync.Components
{
    public partial class EndpointPathPanel : CompositeDrawable
    {
        private readonly Bindable<string> pathBindable;

        public Action? BrowseRequested;

        public EndpointPathPanel(string endpointLabel, Bindable<string> pathBindable)
        {
            this.pathBindable = pathBindable;

            RelativeSizeAxes = Axes.X;
            Height = EzRealmSyncLayout.PATH_ROW_HEIGHT;

            var input = new EzLabelledTextBox(endpointLabel) { Current = { BindTarget = pathBindable } };

            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                ColumnDimensions = new[]
                {
                    new Dimension(),
                    new Dimension(GridSizeMode.Absolute, EzRealmSyncLayout.BROWSE_BUTTON_WIDTH),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        input,
                        new EzButton
                        {
                            Text = "浏览…",
                            Action = () => BrowseRequested?.Invoke(),
                        }.Fill(),
                    },
                },
            };
        }
    }
}
