using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace osu.EzRealmSync.Components
{
    /// <summary>
    /// A/B 双端路径区：固定行高网格，避免垂直 Flow 重叠。
    /// </summary>
    public partial class EndpointPairPanel : CompositeDrawable
    {
        public EndpointPairPanel(
            Bindable<string> endpointAPath,
            Bindable<string> endpointBPath,
            Action browseEndpointA,
            Action browseEndpointB)
        {
            RelativeSizeAxes = Axes.X;
            Height = EzRealmSyncLayout.PATH_ROW_HEIGHT * 2 + 8;

            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                RowDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, EzRealmSyncLayout.PATH_ROW_HEIGHT),
                    new Dimension(GridSizeMode.Absolute, EzRealmSyncLayout.PATH_ROW_HEIGHT),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        new EndpointPathPanel("A 端", endpointAPath) { BrowseRequested = browseEndpointA },
                    },
                    new Drawable[]
                    {
                        new EndpointPathPanel("B 端", endpointBPath) { BrowseRequested = browseEndpointB },
                    },
                },
            };
        }
    }
}
