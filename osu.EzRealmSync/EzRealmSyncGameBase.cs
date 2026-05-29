using osu.EzRealmSync.Platform;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace osu.EzRealmSync
{
    /// <summary>
    /// 纯 osu.Framework 宿主（不引用 osu.Game / osu.Game.Resources）。
    /// </summary>
    public partial class EzRealmSyncGameBase : Framework.Game
    {
        protected override Container<Drawable> Content { get; }

        protected EzRealmSyncGameBase()
        {
            base.Content.Add(Content = new DrawSizePreservingFillContainer
            {
                RelativeSizeAxes = Axes.Both,
                TargetDrawSize = new Vector2(1280, 720),
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            EzSystemFontSetup.Load(Host.Renderer, Fonts, AddFont);
        }
    }
}
