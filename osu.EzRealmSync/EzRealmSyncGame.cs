using osu.EzRealmSync.Screens;
using osu.EzRealmSync.UI;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Handlers.Mouse;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.EzRealmSync;
using osu.Game.EzRealmSync.Abstractions;

namespace osu.EzRealmSync
{
    /// <summary>
    /// 独立工具：仅 osu.Framework + 本仓 UI，无 osu.Game.Resources。
    /// </summary>
    public partial class EzRealmSyncGame : EzRealmSyncGameBase
    {
        private ScreenStack screenStack = null!;

        [Cached(typeof(IEzRealmSyncDialogs))]
        private readonly EzDialogOverlay dialogOverlay = new EzDialogOverlay();

        public EzRealmSyncGame(EzRealmSyncLaunchOptions launchOptions)
        {
            LaunchOptions = launchOptions;
        }

        public EzRealmSyncLaunchOptions LaunchOptions { get; }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

            IEzRealmSyncService service = EzRealmSyncServiceFactory.Create(
                LaunchOptions.UiTestMode,
                LaunchOptions.MockOptions);

            dependencies.CacheAs(service);
            dependencies.CacheAs(LaunchOptions);
            dependencies.CacheAs<IEzRealmSyncDialogs>(dialogOverlay);

            return dependencies;
        }

        public override void SetHost(GameHost host)
        {
            base.SetHost(host);

            if (host.Window != null)
            {
                host.Window.Title = LaunchOptions.UiTestMode
                    ? "Ez Realm Sync [UI Test]"
                    : "Ez Realm Sync";
            }

            var mouseHandler = host.AvailableInputHandlers.OfType<MouseHandler>().FirstOrDefault();
            if (mouseHandler != null)
                mouseHandler.UseRelativeMode.Value = false;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Audio.Volume.Value = 0;

            Content.Add(dialogOverlay);
            Content.Add(screenStack = new ScreenStack { RelativeSizeAxes = Axes.Both });
            screenStack.Push(new EzRealmSyncScreen());
        }
    }
}
