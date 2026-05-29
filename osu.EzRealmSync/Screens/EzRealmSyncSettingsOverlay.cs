// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzRealmSync.Mock;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.EzRealmSync.Screens
{
    public partial class EzRealmSyncSettingsOverlay : OsuFocusedOverlayContainer
    {
        private readonly BindableBool uiTestMode;
        private readonly MockEzRealmSyncService? mockService;

        public Action? CloseRequested;

        public EzRealmSyncSettingsOverlay(BindableBool uiTestMode, MockEzRealmSyncService? mockService)
        {
            this.uiTestMode = uiTestMode;
            this.mockService = mockService;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            RelativeSizeAxes = Axes.Both;

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
                    Size = new Vector2(520, 480),
                    Masking = true,
                    CornerRadius = 10,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colours.Gray0,
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding(20),
                            Spacing = new Vector2(0, 12),
                            Direction = FillDirection.Vertical,
                            Children = createContent(),
                        },
                    },
                },
            };
        }

        private Drawable[] createContent()
        {
            var children = new List<Drawable>
            {
                new OsuSpriteText
                {
                    Text = "设置",
                    Font = OsuFont.GetFont(size: 24, weight: FontWeight.Bold),
                },
                new SettingsCheckbox
                {
                    LabelText = "UI 测试模式",
                    Current = uiTestMode,
                },
                new OsuSpriteText
                {
                    Text = "关闭 UI 测试模式并连接真实数据库需重启应用（Phase 2）。",
                    Font = OsuFont.GetFont(size: 12),
                    Colour = Colour4.Gray,
                },
                new RoundedButton
                {
                    Text = "关闭",
                    Action = () => CloseRequested?.Invoke(),
                },
            };

            if (mockService != null)
            {
                children.Insert(3, new OsuSpriteText
                {
                    Text = "模拟选项（仅 UI 测试模式）",
                    Font = OsuFont.GetFont(weight: FontWeight.SemiBold),
                });

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
            };

            flow.Add(new OsuSpriteText
            {
                Text = label,
                Width = 100,
                Font = OsuFont.GetFont(size: 14),
            });

            foreach (T value in Enum.GetValues<T>())
            {
                var captured = value;
                flow.Add(new RoundedButton
                {
                    Text = value.ToString(),
                    Width = 100,
                    Action = () => setter(captured),
                });
            }

            return flow;
        }

        protected override void PopIn() => this.FadeIn(200);

        protected override void PopOut() => this.FadeOut(200);
    }
}
