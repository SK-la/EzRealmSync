using osu.EzRealmSync.UI;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.EzRealmSync.Models;
using osuTK;

namespace osu.EzRealmSync.Components
{
    public partial class DiffCategoryTabBar : CompositeDrawable
    {
        public Bindable<DiffCategory> Current { get; } = new Bindable<DiffCategory>(DiffCategory.SourceOnly);

        private readonly Bindable<SyncDirection> direction;

        private readonly DiffCategoryTabItem[] tabItems = new DiffCategoryTabItem[3];

        public DiffCategoryTabBar(Bindable<SyncDirection> direction)
        {
            this.direction = direction;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            var tabsFlow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(20, 0),
                Children = new Drawable[]
                {
                    tabItems[0] = new DiffCategoryTabItem(DiffCategory.SourceOnly),
                    tabItems[1] = new DiffCategoryTabItem(DiffCategory.TargetOnly),
                    tabItems[2] = new DiffCategoryTabItem(DiffCategory.Conflicted),
                },
            };

            InternalChild = new BasicScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                ScrollbarVisible = false,
                Child = tabsFlow,
            };

            foreach (var tab in tabItems)
                tab.Action = () => Current.Value = tab.Category;

            Current.BindValueChanged(category =>
            {
                foreach (var tab in tabItems)
                    tab.Active = tab.Category == category.NewValue;
            }, true);

            direction.BindValueChanged(_ => updateTabLabels(), true);
        }

        private void updateTabLabels()
        {
            getEndpointLabels(direction.Value, out string source, out string target);

            tabItems[0].Label = $"{source} 端有 · {target} 端无";
            tabItems[1].Label = $"{target} 端有 · {source} 端无";
            tabItems[2].Label = "不一致";
        }

        public static void getEndpointLabels(SyncDirection direction, out string source, out string target)
        {
            if (direction == SyncDirection.EzToOfficial)
            {
                source = "A";
                target = "B";
            }
            else
            {
                source = "B";
                target = "A";
            }
        }

        private partial class DiffCategoryTabItem : CompositeDrawable
        {
            public readonly DiffCategory Category;

            public Action? Action;

            private readonly EzText labelText;
            private readonly Box underline;

            private bool active;

            public bool Active
            {
                get => active;
                set
                {
                    if (active == value)
                        return;

                    active = value;
                    updateVisualState();
                }
            }

            public string Label
            {
                get => labelText.Text.ToString();
                set => labelText.Text = value;
            }

            public DiffCategoryTabItem(DiffCategory category)
            {
                Category = category;
                AutoSizeAxes = Axes.X;
                Height = EzRealmSyncLayout.DIFF_TAB_BAR_HEIGHT;

                InternalChildren = new Drawable[]
                {
                    labelText = new EzText().WithSize(14, "SemiBold"),
                    underline = new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 2,
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Alpha = 0,
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                updateVisualState();
            }

            protected override bool OnClick(ClickEvent e)
            {
                Action?.Invoke();
                return true;
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (!active)
                    labelText.Colour = Colour4.White;
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                if (!active)
                    updateVisualState();
            }

            private void updateVisualState()
            {
                labelText.Colour = active ? EzTheme.Accent : EzTheme.AccentSecondary;
                underline.Colour = EzTheme.Accent;
                underline.Alpha = active ? 1 : 0;
            }
        }
    }
}
