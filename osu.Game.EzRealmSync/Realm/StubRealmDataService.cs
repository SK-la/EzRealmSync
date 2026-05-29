using osu.Game.EzRealmSync.Abstractions;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    public sealed class StubRealmDataService : IRealmDataService
    {
        public Task<IReadOnlyList<RealmFileEntry>> DiscoverRealmFilesAsync(string? searchDirectory, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RealmFileEntry>>(Array.Empty<RealmFileEntry>());

        public Task<RealmFileEntry> RegisterRealmFileAsync(string realmFilePath, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("未检测到 lib/osu.Game.dll。请使用 --ui-test，或将官方 osu.Game 放入 lib/。");

        public Task<RealmSnapshot> LoadRealmSnapshotAsync(string realmId, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("未检测到 lib/osu.Game.dll。请使用 --ui-test，或将官方 osu.Game 放入 lib/。");

        public Task<string> CreateTimestampedBackupAsync(string realmFilePath, string backupDirectory, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("未检测到 lib/osu.Game.dll。请使用 --ui-test，或将官方 osu.Game 放入 lib/。");

        public Task<ScanResult> CompareRealmSetsAsync(
            RealmSetOperation operation,
            string sourceRealmId,
            string targetRealmId,
            EntityKindFilter entityFilter,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("未检测到 lib/osu.Game.dll。请使用 --ui-test，或将官方 osu.Game 放入 lib/。");
    }
}
