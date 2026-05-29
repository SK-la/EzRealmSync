using osu.EzRealmSync.UI;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzRealmSync.Models;
using osuTK;

namespace osu.EzRealmSync.Components
{
    public partial class SyncSidebarPanel : CompositeDrawable
    {
        public Bindable<SyncDirection> Direction { get; } = new Bindable<SyncDirection>(SyncDirection.EzToOfficial);

        public Bindable<EntityKindFilter> EntityFilter { get; } = new Bindable<EntityKindFilter>(EntityKindFilter.All);

        private readonly List<EzButton> entityButtons = new List<EzButton>();
        private readonly EzButton directionAToB = null!;
        private readonly EzButton directionBToA = null!;

        public SyncSidebarPanel()
        {
            RelativeSizeAxes = Axes.Both;

            var content = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 10),
                Padding = new MarginPadding(EzRealmSyncLayout.CONTENT_PADDING),
                Children = new List<Drawable>
                {
                    new EzText { Text = "同步方向" }.WithSize(14, "Bold"),
                },
            };

            content.Add(new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
                Children = new Drawable[]
                {
                    directionAToB = createButton("A → B", () => Direction.Value = SyncDirection.EzToOfficial),
                    directionBToA = createButton("B → A", () => Direction.Value = SyncDirection.OfficialToEz),
                },
            });

            content.Add(new EzText { Text = "数据类型", Margin = new MarginPadding { Top = 8 } }.WithSize(14, "Bold"));

            var entityFlow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
            };

            foreach (EntityKindFilter filter in Enum.GetValues<EntityKindFilter>())
            {
                var captured = filter;
                var button = createButton(EnumDescriptions.Get(captured), () => EntityFilter.Value = captured);
                entityButtons.Add(button);
                entityFlow.Add(button);
            }

            content.Add(entityFlow);
            content.Add(new EzText
            {
                Text = "收藏夹 (Phase 2)",
                Colour = EzTheme.TextMuted,
                Alpha = 0.5f,
                Margin = new MarginPadding { Top = 8 },
            }.WithSize(12));

            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = EzTheme.PanelDark },
                new BasicScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = content,
                },
            };

            Direction.BindValueChanged(_ => updateDirectionButtons(), true);
            EntityFilter.BindValueChanged(_ => updateEntityButtons(), true);
        }

        private static EzButton createButton(string text, Action action)
        {
            var button = new EzButton
            {
                Text = text,
                Action = action,
            };
            button.SizeToText();
            return button;
        }

        private void updateDirectionButtons()
        {
            bool aToB = Direction.Value == SyncDirection.EzToOfficial;
            directionAToB.Alpha = aToB ? 1f : 0.55f;
            directionBToA.Alpha = aToB ? 0.55f : 1f;
        }

        private void updateEntityButtons()
        {
            int index = 0;

            foreach (EntityKindFilter filter in Enum.GetValues<EntityKindFilter>())
            {
                entityButtons[index].Alpha = EntityFilter.Value == filter ? 1f : 0.55f;
                index++;
            }
        }
    }
}
