// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzRealmSync.Models;
using osu.Game.Graphics.Containers;

namespace osu.EzRealmSync.Components
{
    public partial class DiffListPanel : CompositeDrawable
    {
        private readonly List<DiffItem> items;

        private FillFlowContainer<DiffListRow> flow = null!;

        public DiffListPanel(IEnumerable<DiffItem> items)
        {
            this.items = items.ToList();
        }

        public IEnumerable<DiffItem> GetSelectedItems()
        {
            return flow.Children.Where(r => r.Selected.Value).Select(r => r.Item);
        }

        public void SelectAll(bool selected)
        {
            foreach (var row in flow.Children)
                row.Selected.Value = selected;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChild = new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = flow = new FillFlowContainer<DiffListRow>
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Children = items.Select(i => new DiffListRow(i)).ToArray(),
                },
            };
        }
    }
}
