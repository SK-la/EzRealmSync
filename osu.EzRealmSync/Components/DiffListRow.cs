// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzRealmSync.Models;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;

namespace osu.EzRealmSync.Components
{
    public partial class DiffListRow : CompositeDrawable
    {
        public readonly DiffItem Item;

        public BindableBool Selected { get; } = new BindableBool();

        public DiffListRow(DiffItem item)
        {
            Item = item;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            Height = 36;

            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                Height = 36,
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, 36),
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
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = new OsuCheckbox { Current = Selected },
                        },
                        new OsuSpriteText
                        {
                            Text = Item.Title,
                            Font = OsuFont.GetFont(size: 14),
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Truncate = true,
                        },
                        new OsuSpriteText
                        {
                            Text = Item.Artist,
                            Font = OsuFont.GetFont(size: 14),
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Truncate = true,
                        },
                        new OsuSpriteText
                        {
                            Text = Item.Hash.Length > 12 ? Item.Hash[..12] + "…" : Item.Hash,
                            Font = OsuFont.GetFont(size: 12),
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                        new OsuSpriteText
                        {
                            Text = Item.Ruleset,
                            Font = OsuFont.GetFont(size: 12),
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                        new OsuSpriteText
                        {
                            Text = Item.ConflictSummary ?? (Item.Date?.ToString("yyyy-MM-dd") ?? Item.EntityKind.ToString()),
                            Font = OsuFont.GetFont(size: 12),
                            Colour = Item.ConflictSummary != null ? Colour4.OrangeRed : Colour4.White,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Truncate = true,
                        },
                    },
                },
            };
        }
    }
}
