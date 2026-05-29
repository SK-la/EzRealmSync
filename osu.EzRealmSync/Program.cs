using osu.EzRealmSync.Hosting;

namespace osu.EzRealmSync
{
    /// <summary>
    /// 独立程序入口。本程序不是 osu! 规则集；UI 仅依赖 osu.Framework。
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
