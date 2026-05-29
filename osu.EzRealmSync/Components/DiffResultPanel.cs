using osu.EzRealmSync.UI;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.Components
{
    public partial class DiffResultPanel : CompositeDrawable
    {
        public DiffCategoryTabBar CategoryTabs { get; private set; } = null!;

        public Container ListHost { get; private set; } = null!;

        public EzButton SelectAllButton { get; private set; } = null!;

        public Action? SelectAllRequested;

        private readonly Bindable<SyncDirection> direction;

        public DiffResultPanel(Bindable<SyncDirection> direction)
        {
            this.direction = direction;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            CategoryTabs = new DiffCategoryTabBar(direction);
            SelectAllButton = new EzButton
            {
                Text = "全选",
                Width = 80,
                Action = () => SelectAllRequested?.Invoke(),
            };

            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = EzTheme.Panel },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding(EzRealmSyncLayout.CONTENT_PADDING),
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, EzRealmSyncLayout.DIFF_TAB_BAR_HEIGHT),
                        new Dimension(),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new GridContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                ColumnDimensions = new[]
                                {
                                    new Dimension(),
                                    new Dimension(GridSizeMode.Absolute, 88),
                                },
                                Content = new[]
                                {
                                    new Drawable[] { CategoryTabs, SelectAllButton },
                                },
                            },
                        },
                        new Drawable[]
                        {
                            ListHost = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding { Top = 8 },
                            },
                        },
                    },
                },
            };
        }
    }
}
