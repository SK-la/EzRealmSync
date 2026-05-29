// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.EzRealmSync.Hosting;

namespace osu.EzRealmSync
{
    /// <summary>
    /// 独立程序入口。本程序不是 osu! 规则集；仅通过项目引用复用 osu.Game 的 Framework UI。
    /// </summary>
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var options = EzRealmSyncLaunchOptions.Parse(args);
            EzRealmSyncHostFactory.Create(EzRealmSyncHostKind.Standalone).Run(options);
        }
    }
}
