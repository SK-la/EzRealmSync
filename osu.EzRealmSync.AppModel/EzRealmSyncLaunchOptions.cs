using osu.Game.EzRealmSync.Mock;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.AppModel
{
    public sealed class EzRealmSyncLaunchOptions
    {
        public bool UiTestMode { get; init; }

        /// <summary>命令行是否显式指定了 <c>--ui-test</c> / <c>--no-ui-test</c>（未指定时启动读 settings.json）。</summary>
        public bool HasUiTestModeArgument { get; init; }

        public MockEzRealmSyncOptions MockOptions { get; init; } = new();

        public static EzRealmSyncLaunchOptions Parse(string[] args)
        {
            bool? uiTest = null;
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
                UiTestMode = uiTest ?? false,
                HasUiTestModeArgument = uiTest.HasValue,
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
