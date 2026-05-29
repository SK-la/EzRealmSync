using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;

namespace osu.EzRealmSync.UI
{
    public partial class EzDialogOverlay : CompositeDrawable, IEzRealmSyncDialogs
    {
        public EzDialogOverlay()
        {
            RelativeSizeAxes = Axes.Both;
            Alpha = 0;
        }

        public void PushConfirm(string message, Action onConfirm, Action? onCancel = null) => show(message, onConfirm, onCancel, dangerous: false);

        public void PushDangerous(string message, Action onConfirm, Action? onCancel = null) => show(message, onConfirm, onCancel, dangerous: true);

        private void show(string message, Action onConfirm, Action? onCancel, bool dangerous)
        {
            ClearInternal();

            var panel = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Width = 420,
                AutoSizeAxes = Axes.Y,
                Masking = true,
                CornerRadius = 8,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = EzTheme.Panel,
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding(20),
                        Spacing = new Vector2(0, 12),
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            new EzText { Text = message }.WithSize(16),
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Y,
                                RelativeSizeAxes = Axes.X,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(8, 0),
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                Children = new Drawable[]
                                {
                                    new EzButton
                                    {
                                        Text = "取消",
                                        Action = () =>
                                        {
                                            onCancel?.Invoke();
                                            hide();
                                        },
                                    },
                                    new EzButton
                                    {
                                        Text = dangerous ? "删除" : "确定",
                                        BackgroundColour = dangerous ? Colour4.Red : EzTheme.AccentSecondary,
                                        Action = () =>
                                        {
                                            hide();
                                            onConfirm();
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.Black.Opacity(0.65f),
                },
                panel,
            };

            this.FadeIn(150);
        }

        private void hide()
        {
            ClearInternal();
            Alpha = 0;
        }
    }
}
