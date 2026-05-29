// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework;
using osu.Framework.Platform;

namespace osu.EzRealmSync.Hosting
{
    /// <summary>
    /// 独立 WinExe 宿主：自建 <see cref="DesktopGameHost"/>，不依赖 osu.Desktop 或任何 Ruleset。
    /// </summary>
    public sealed class StandaloneEzRealmSyncHost : IEzRealmSyncHost
    {
        public void Run(EzRealmSyncLaunchOptions options)
        {
            using var host = Host.GetSuitableDesktopHost("EzRealmSync");
            host.Run(new EzRealmSyncGame(options));
        }
    }
}
