using osu.EzRealmSync.UI;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Game.EzRealmSync.Mock;
using osuTK;

namespace osu.EzRealmSync.Screens
{
    public partial class EzRealmSyncSettingsOverlay : CompositeDrawable
    {
        private readonly BindableBool uiTestMode;
        private readonly MockEzRealmSyncService? mockService;

        public Action? CloseRequested;

        public EzRealmSyncSettingsOverlay(BindableBool uiTestMode, MockEzRealmSyncService? mockService)
        {
            this.uiTestMode = uiTestMode;
            this.mockService = mockService;
            RelativeSizeAxes = Axes.Both;
            Alpha = 0;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.Black.Opacity(0.6f),
                },
                new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Width = 520,
                    AutoSizeAxes = Axes.Y,
                    Masking = true,
                    CornerRadius = 10,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = EzTheme.PanelDark,
                        },
                        new BasicScrollContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Child = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Padding = new MarginPadding(20),
                                Spacing = new Vector2(0, 12),
                                Direction = FillDirection.Vertical,
                                Children = createContent(),
                            },
                        },
                    },
                },
            };
        }

        private Drawable[] createContent()
        {
            var children = new List<Drawable>
            {
                new EzText { Text = "设置" }.WithSize(24, "Bold"),
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Y,
                    RelativeSizeAxes = Axes.X,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(8, 0),
                    Children = new Drawable[]
                    {
                        new BasicCheckbox { Current = { BindTarget = uiTestMode } },
                        new EzText { Text = "UI 测试模式" },
                    },
                },
                new EzText
                {
                    Text = "关闭 UI 测试模式并连接真实数据库需重启应用（Phase 2）。",
                    Colour = EzTheme.TextMuted,
                }.WithSize(12),
                new EzButton
                {
                    Text = "关闭",
                    Action = () => CloseRequested?.Invoke(),
                },
            };

            if (mockService != null)
            {
                children.Insert(3, new EzText { Text = "模拟选项（仅 UI 测试模式）" }.WithSize(14, "SemiBold"));
                children.Insert(4, createEnumRow("数据集", mockService.Options.DatasetSize, v => mockService.Options.DatasetSize = v));
                children.Insert(5, createEnumRow("错误注入", mockService.Options.ErrorInjection, v => mockService.Options.ErrorInjection = v));
            }

            return children.ToArray();
        }

        private Drawable createEnumRow<T>(string label, T current, Action<T> setter) where T : struct, Enum
        {
            var flow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Y,
                RelativeSizeAxes = Axes.X,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                Children = new Drawable[]
                {
                    new EzText
                    {
                        Text = label,
                        Width = 100,
                    }.WithSize(14),
                },
            };

            foreach (T value in Enum.GetValues<T>())
            {
                var captured = value;
                flow.Add(new EzButton
                {
                    Text = EnumDescriptions.Get(captured),
                    Width = 100,
                    Action = () => setter(captured),
                });
            }

            return flow;
        }

        public void OpenOverlay() => this.FadeIn(200);

        public void CloseOverlay() => this.FadeOut(200);
    }
}
