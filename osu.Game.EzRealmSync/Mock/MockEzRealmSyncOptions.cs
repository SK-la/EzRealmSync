using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Mock
{
    public sealed class MockEzRealmSyncOptions
    {
        public MockDatasetSize DatasetSize { get; set; } = MockDatasetSize.Medium;

        public MockErrorInjection ErrorInjection { get; set; } = MockErrorInjection.None;

        /// <summary>
        /// Simulated scan/apply delay. Set to 0 for instant feedback.
        /// </summary>
        public int SimulatedDelayMilliseconds { get; set; } = 1500;
    }
}
