// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.EzRealmSync.Screens;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Handlers.Mouse;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game;
using osu.Game.EzRealmSync.Abstractions;
using osu.Game.EzRealmSync.Mock;
using osu.Game.EzRealmSync.Realm;
using osu.Game.Overlays;

namespace osu.EzRealmSync
{
    /// <summary>
    /// 独立工具用的轻量 <see cref="OsuGameBase"/> 宿主（非 <see cref="osu.Game.Rulesets.Ruleset"/>，不挂入 osu.Desktop）。
    /// </summary>
    public partial class EzRealmSyncGame : OsuGameBase
    {
        private ScreenStack screenStack = null!;

        [Cached(typeof(IDialogOverlay))]
        private readonly DialogOverlay dialogOverlay = new DialogOverlay();

        public EzRealmSyncGame(EzRealmSyncLaunchOptions launchOptions)
        {
            this.LaunchOptions = launchOptions;
        }

        public EzRealmSyncLaunchOptions LaunchOptions { get; }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

            IEzRealmSyncService service = LaunchOptions.UiTestMode
                ? new MockEzRealmSyncService(LaunchOptions.MockOptions)
                : new StubRealmEzRealmSyncService();

            dependencies.CacheAs(service);
            dependencies.CacheAs(LaunchOptions);

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

            Add(dialogOverlay);
            Add(screenStack = new ScreenStack { RelativeSizeAxes = Axes.Both });
            screenStack.Push(new EzRealmSyncScreen());
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
        }
    }
}
