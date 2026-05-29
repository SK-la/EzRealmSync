// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;

namespace osu.EzRealmSync.Components
{
    public partial class PathInputRow : CompositeDrawable
    {
        private readonly Bindable<string> pathBindable;
        private readonly string labelText;

        public Action? BrowseRequested;

        private OsuTextBox textBox = null!;

        public PathInputRow(string label, Bindable<string> pathBindable)
        {
            labelText = label;
            this.pathBindable = pathBindable;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, 140),
                    new Dimension(),
                    new Dimension(GridSizeMode.Absolute, 100),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = labelText,
                            Font = OsuFont.GetFont(weight: FontWeight.SemiBold),
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                        textBox = new OsuTextBox
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = pathBindable.Value,
                        },
                        new RoundedButton
                        {
                            Text = "浏览…",
                            RelativeSizeAxes = Axes.X,
                            Action = () => BrowseRequested?.Invoke(),
                        },
                    },
                },
            };

            textBox.Current.BindTarget = pathBindable;
        }
    }
}
