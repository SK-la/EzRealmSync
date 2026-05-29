using osu.EzRealmSync.UI;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.Components
{
    public partial class DiffListRow : CompositeDrawable
    {
        public readonly DiffItem Item;

        public readonly int Index;

        public BindableBool Selected { get; } = new BindableBool();

        private Box background = null!;

        public DiffListRow(DiffItem item, int index)
        {
            Item = item;
            Index = index;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            Height = 36;

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = EzTheme.AccentSecondary,
                    Alpha = 0,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Horizontal = 8 },
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Distributed, 2),
                        new Dimension(GridSizeMode.Distributed, 1.5f),
                        new Dimension(GridSizeMode.Absolute, 120),
                        new Dimension(GridSizeMode.Absolute, 70),
                        new Dimension(GridSizeMode.Distributed, 1.5f),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new EzText { Text = Item.Title },
                            new EzText { Text = Item.Artist },
                            new EzText { Text = Item.Hash.Length > 12 ? Item.Hash[..12] + "…" : Item.Hash }.WithSize(12),
                            new EzText { Text = Item.Ruleset }.WithSize(12),
                            new EzText
                            {
                                Text = Item.ConflictSummary ?? Item.Date?.ToString("yyyy-MM-dd") ?? Item.EntityKind.ToString(),
                                Colour = Item.ConflictSummary != null ? Colour4.OrangeRed : EzTheme.Text,
                            }.WithSize(12),
                        },
                    },
                },
            };

            Selected.BindValueChanged(selected => background.Alpha = selected.NewValue ? 0.45f : 0, true);
        }
    }
}
