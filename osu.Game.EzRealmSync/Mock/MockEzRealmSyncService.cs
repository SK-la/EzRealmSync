using osu.Game.EzRealmSync.Abstractions;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Mock
{
    public sealed partial class MockEzRealmSyncService : IEzRealmSyncService
    {
        private ScanResult currentResult = new ScanResult();
        private readonly List<BackupEntry> backups = new List<BackupEntry>();

        public MockEzRealmSyncService(MockEzRealmSyncOptions? options = null)
        {
            Options = options ?? new MockEzRealmSyncOptions();
            seedBackups();
        }

        public MockEzRealmSyncOptions Options { get; }

        public Task<ValidationResult> ValidatePathsAsync(PathConfiguration paths, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(Options.ErrorInjection switch
            {
                MockErrorInjection.InvalidPath => ValidationResult.Failure("路径无效（模拟）。"),
                MockErrorInjection.ProcessLocked => ValidationResult.Failure("client.realm 正被占用，请关闭 osu! / Ez2Lazer（模拟）。"),
                _ => ValidationResult.Success(),
            });
        }

        public async Task<ScanResult> ScanAsync(ScanRequest request, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            if (Options.ErrorInjection == MockErrorInjection.ScanCancelled)
                throw new OperationCanceledException("扫描已取消（模拟）。");

            if (Options.ErrorInjection == MockErrorInjection.ProcessLocked)
                throw new InvalidOperationException("client.realm 正被占用，请关闭 osu! / Ez2Lazer（模拟）。");

            if (Options.ErrorInjection == MockErrorInjection.InvalidPath)
                throw new InvalidOperationException("路径无效（模拟）。");

            await simulateWorkAsync(progress, "正在扫描差异…", cancellationToken).ConfigureAwait(false);

            currentResult = generateDataset(Options.DatasetSize);
            return currentResult;
        }

        public async Task<ApplyResult> ApplyAsync(ApplyRequest request, IProgress<ApplyProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            await simulateWorkAsync(
                p => progress?.Report(new ApplyProgress { Progress = p, Message = request.DeleteFromSource ? "正在从源库删除…" : "正在写入目标库…" }),
                request.DeleteFromSource ? "正在从源库删除…" : "正在写入目标库…",
                cancellationToken).ConfigureAwait(false);

            var ids = request.ItemIds.ToHashSet();
            string? backupPath = null;

            if (request.CreateBackup && !request.DeleteFromSource)
            {
                backupPath = $@"C:\Fake\backups\{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}";
                backups.Insert(0, new BackupEntry
                {
                    Id = Guid.NewGuid().ToString("N"),
                    CreatedAt = DateTimeOffset.UtcNow,
                    Description = $"apply ({request.Direction})",
                    Path = backupPath,
                });
            }

            if (request.DeleteFromSource)
            {
                currentResult = new ScanResult
                {
                    SourceOnly = currentResult.SourceOnly.Where(i => !ids.Contains(i.Id)).ToList(),
                    TargetOnly = currentResult.TargetOnly,
                    Conflicted = currentResult.Conflicted,
                };
            }
            else
            {
                var moved = currentResult.SourceOnly.Where(i => ids.Contains(i.Id)).ToList();
                currentResult = new ScanResult
                {
                    SourceOnly = currentResult.SourceOnly.Where(i => !ids.Contains(i.Id)).ToList(),
                    TargetOnly = currentResult.TargetOnly,
                    Conflicted = currentResult.Conflicted,
                };
            }

            return new ApplyResult
            {
                AppliedCount = ids.Count,
                BackupPath = backupPath,
            };
        }

        public Task<IReadOnlyList<BackupEntry>> ListBackupsAsync(string? backupDirectory = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<BackupEntry>>(backups.ToList());
        }

        public async Task RestoreBackupAsync(
            string backupId,
            string targetRealmFilePath,
            string? backupDirectory = null,
            string? safetyBackupDirectory = null,
            IProgress<ApplyProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (backups.All(b => b.Id != backupId))
                throw new InvalidOperationException($"找不到备份 {backupId}");

            await simulateWorkAsync(
                p => progress?.Report(new ApplyProgress { Progress = p, Message = "正在还原备份…" }),
                "正在还原备份…",
                cancellationToken).ConfigureAwait(false);
        }

        private void seedBackups()
        {
            for (int i = 0; i < 3; i++)
            {
                backups.Add(new BackupEntry
                {
                    Id = Guid.NewGuid().ToString("N"),
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-i),
                    Description = $"mock-backup-{i}",
                    Path = $@"C:\Fake\backups\mock_{i}",
                });
            }
        }

        private async Task simulateWorkAsync(IProgress<ScanProgress>? progress, string message, CancellationToken cancellationToken)
        {
            await simulateWorkAsync(p => progress?.Report(new ScanProgress { Progress = p, Message = message }), message, cancellationToken).ConfigureAwait(false);
        }

        private async Task simulateWorkAsync(Action<double> report, string message, CancellationToken cancellationToken)
        {
            int delay = Options.SimulatedDelayMilliseconds;

            if (delay <= 0)
            {
                report(1);
                return;
            }

            const int steps = 20;

            for (int i = 0; i <= steps; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                report(i / (double)steps);
                await Task.Delay(delay / steps, cancellationToken).ConfigureAwait(false);
            }
        }

        private static ScanResult generateDataset(MockDatasetSize size)
        {
            return size switch
            {
                MockDatasetSize.Empty => new ScanResult(),
                MockDatasetSize.Large => new ScanResult
                {
                    SourceOnly = generateItems(DiffCategory.SourceOnly, 500),
                    TargetOnly = generateItems(DiffCategory.TargetOnly, 40),
                    Conflicted = generateItems(DiffCategory.Conflicted, 15, conflicted: true),
                },
                _ => new ScanResult
                {
                    SourceOnly = generateItems(DiffCategory.SourceOnly, 32),
                    TargetOnly = generateItems(DiffCategory.TargetOnly, 12),
                    Conflicted = generateItems(DiffCategory.Conflicted, 5, conflicted: true),
                },
            };
        }

        private static List<DiffItem> generateItems(DiffCategory category, int count, bool conflicted = false)
        {
            var list = new List<DiffItem>(count);
            var kinds = new[] { EntityKind.BeatmapSet, EntityKind.Beatmap, EntityKind.Score, EntityKind.BeatmapCollection };

            for (int i = 0; i < count; i++)
            {
                var kind = kinds[i % kinds.Length];
                list.Add(new DiffItem
                {
                    Id = Guid.NewGuid(),
                    Category = category,
                    EntityKind = kind,
                    Title = $"Mock {kind} #{i + 1}",
                    Artist = "Mock Artist",
                    Hash = Convert.ToHexString(Guid.NewGuid().ToByteArray())[..32].ToLowerInvariant(),
                    Ruleset = (i % 4) switch
                    {
                        0 => "osu",
                        1 => "mania",
                        2 => "taiko",
                        _ => "catch"
                    },
                    Date = kind == EntityKind.Score ? DateTimeOffset.UtcNow.AddDays(-i) : null,
                    ConflictSummary = conflicted ? "StarRating 不一致 (mock)" : null,
                });
            }

            return list;
        }
    }
}
