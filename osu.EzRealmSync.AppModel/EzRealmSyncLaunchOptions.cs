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
            bool uiTest = false;
            var mock = new MockEzRealmSyncOptions();

            foreach (string arg in args)
            {
                if (arg is "--ui-test" or "-ui-test")
                    uiTest = true;
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
                EzDataPath = @"C:\Fake\Ez2Lazer\data",
                OfficialDataPath = @"C:\Fake\osu\data",
            };
        }
    }
}
