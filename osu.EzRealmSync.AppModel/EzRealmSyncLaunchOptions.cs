using osu.Game.EzRealmSync.Mock;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.AppModel
{
    public sealed class EzRealmSyncLaunchOptions
    {
        public bool UiTestMode { get; init; }

        public MockEzRealmSyncOptions MockOptions { get; init; } = new();

        public static EzRealmSyncLaunchOptions Parse(string[] args)
        {
            // 默认连接真实 Realm；仅 UI 布局调试时加 --ui-test。
            bool uiTest = false;
            var mock = new MockEzRealmSyncOptions();

            foreach (string arg in args)
            {
                if (arg is "--ui-test" or "-ui-test")
                    uiTest = true;
                else if (arg is "--no-ui-test" or "-no-ui-test")
                    uiTest = false;
                else if (arg.StartsWith("--mock-delay=", StringComparison.OrdinalIgnoreCase) && int.TryParse(arg.Split('=')[1], out int delay))
                    mock.SimulatedDelayMilliseconds = delay;
            }

            return new EzRealmSyncLaunchOptions
            {
                UiTestMode = uiTest,
                MockOptions = mock,
            };
        }

        public PathConfiguration CreateDefaultPaths()
        {
            if (!UiTestMode)
                return new PathConfiguration();

            return new PathConfiguration
            {
                EzDataPath = @"C:\Fake\Ez2Lazer",
                OfficialDataPath = @"C:\Fake\osu\data",
            };
        }
    }
}
