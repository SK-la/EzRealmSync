using osu.Game.EzRealmSync.Abstractions;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Mock
{
    public sealed partial class MockEzRealmSyncService : IRealmDataService
    {
        private readonly Dictionary<string, RealmFileEntry> realmFiles = new();
        private readonly Dictionary<string, RealmSnapshot> loadedSnapshots = new();

        public Task<IReadOnlyList<RealmFileEntry>> DiscoverRealmFilesAsync(string? searchDirectory, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            mergeDiscoveredRealmFiles(searchDirectory);

            if (realmFiles.Count == 0)
                seedRealmFiles(searchDirectory);

            return Task.FromResult<IReadOnlyList<RealmFileEntry>>(realmFiles.Values.OrderBy(f => f.DisplayName).ToList());
        }

        private void mergeDiscoveredRealmFiles(string? searchDirectory)
        {
            foreach (string path in RealmWorkspacePaths.FindRealmFiles(searchDirectory))
            {
                string fullPath = Path.GetFullPath(path);
                if (realmFiles.Values.Any(f => string.Equals(f.FilePath, fullPath, StringComparison.OrdinalIgnoreCase)))
                    continue;

                string id = Guid.NewGuid().ToString("N");
                realmFiles[id] = new RealmFileEntry
                {
                    Id = id,
                    DisplayName = Path.GetFileName(fullPath),
                    FilePath = fullPath,
                    DataDirectory = RealmWorkspacePaths.ResolveDataDirectory(fullPath),
                    SchemaVersion = 51_003,
                    FileSizeBytes = new FileInfo(fullPath).Length,
                    IsLocked = false,
                };
            }
        }

        public Task<RealmFileEntry> RegisterRealmFileAsync(string realmFilePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string fullPath = Path.GetFullPath(realmFilePath);
            string id = Guid.NewGuid().ToString("N");

            var entry = new RealmFileEntry
            {
                Id = id,
                DisplayName = Path.GetFileName(fullPath),
                FilePath = fullPath,
                DataDirectory = Path.GetDirectoryName(fullPath),
                SchemaVersion = 51_003,
                FileSizeBytes = File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0,
                IsLocked = false,
            };

            realmFiles[id] = entry;
            return Task.FromResult(entry);
        }

        public async Task<RealmSnapshot> LoadRealmSnapshotAsync(string realmId, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            if (!realmFiles.TryGetValue(realmId, out var file))
                throw new InvalidOperationException($"未找到 Realm 文件：{realmId}");

            await simulateWorkAsync(progress, $"正在加载 {file.DisplayName}…", cancellationToken).ConfigureAwait(false);

            var snapshot = MockRealmSnapshotBuilder.Build(file, Options.DatasetSize);
            loadedSnapshots[realmId] = snapshot;
            return snapshot;
        }

        public Task<string> CreateTimestampedBackupAsync(string realmFilePath, string backupDirectory, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string stamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = Path.GetFileName(realmFilePath);
            string backupPath = Path.Combine(backupDirectory, $"{Path.GetFileNameWithoutExtension(fileName)}_{stamp}{Path.GetExtension(fileName)}");

            if (!Directory.Exists(backupDirectory))
                Directory.CreateDirectory(backupDirectory);

            if (File.Exists(realmFilePath))
                File.Copy(realmFilePath, backupPath, overwrite: false);
            else
                File.WriteAllText(backupPath, $"mock backup of {realmFilePath}");

            backups.Insert(0, new BackupEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTimeOffset.UtcNow,
                Description = $"backup {fileName}",
                Path = backupPath,
            });

            return Task.FromResult(backupPath);
        }

        public async Task<ScanResult> CompareRealmSetsAsync(
            RealmSetOperation operation,
            string sourceRealmId,
            string targetRealmId,
            EntityKindFilter entityFilter,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var source = await ensureLoadedAsync(sourceRealmId, progress, cancellationToken).ConfigureAwait(false);
            var target = await ensureLoadedAsync(targetRealmId, progress, cancellationToken).ConfigureAwait(false);

            await simulateWorkAsync(progress, "正在计算集合…", cancellationToken).ConfigureAwait(false);

            var sourceItems = flattenSnapshot(source, entityFilter);
            var targetItems = flattenSnapshot(target, entityFilter);

            var sourceHashes = sourceItems.ToLookup(i => i.Hash);
            var targetHashes = targetItems.ToLookup(i => i.Hash);

            var sourceOnly = new List<DiffItem>();
            var targetOnly = new List<DiffItem>();
            var conflicted = new List<DiffItem>();

            switch (operation)
            {
                case RealmSetOperation.Intersection:
                    foreach (var hash in sourceHashes.Select(g => g.Key).Intersect(targetHashes.Select(g => g.Key)))
                    {
                        var a = sourceHashes[hash].First();
                        var b = targetHashes[hash].First();
                        if (!string.Equals(a.Title, b.Title, StringComparison.Ordinal) || !string.Equals(a.Ruleset, b.Ruleset, StringComparison.Ordinal))
                            conflicted.Add(toDiffItem(b, DiffCategory.Conflicted, true));
                        else
                            sourceOnly.Add(toDiffItem(a, DiffCategory.SourceOnly));
                    }

                    break;

                case RealmSetOperation.Union:
                    var allHashes = sourceHashes.Select(g => g.Key).Union(targetHashes.Select(g => g.Key));

                    foreach (var hash in allHashes)
                    {
                        bool inA = sourceHashes.Contains(hash);
                        bool inB = targetHashes.Contains(hash);

                        if (inA && inB)
                        {
                            var a = sourceHashes[hash].First();
                            var b = targetHashes[hash].First();
                            if (!string.Equals(a.Title, b.Title, StringComparison.Ordinal))
                                conflicted.Add(toDiffItem(b, DiffCategory.Conflicted, true));
                            else
                                sourceOnly.Add(toDiffItem(a, DiffCategory.SourceOnly));
                        }
                        else if (inA)
                            sourceOnly.Add(toDiffItem(sourceHashes[hash].First(), DiffCategory.SourceOnly));
                        else
                            targetOnly.Add(toDiffItem(targetHashes[hash].First(), DiffCategory.TargetOnly));
                    }

                    break;

                case RealmSetOperation.SymmetricDifference:
                    foreach (var hash in sourceHashes.Select(g => g.Key).Union(targetHashes.Select(g => g.Key)))
                    {
                        bool inA = sourceHashes.Contains(hash);
                        bool inB = targetHashes.Contains(hash);
                        if (inA == inB)
                            continue;

                        if (inA)
                            sourceOnly.Add(toDiffItem(sourceHashes[hash].First(), DiffCategory.SourceOnly));
                        else
                            targetOnly.Add(toDiffItem(targetHashes[hash].First(), DiffCategory.TargetOnly));
                    }

                    break;

                default:
                    foreach (var item in sourceItems)
                    {
                        if (!targetHashes.Contains(item.Hash))
                            sourceOnly.Add(toDiffItem(item, DiffCategory.SourceOnly));
                    }

                    foreach (var item in targetItems)
                    {
                        if (!sourceHashes.Contains(item.Hash))
                            targetOnly.Add(toDiffItem(item, DiffCategory.TargetOnly));
                    }

                    foreach (var hash in sourceHashes.Select(g => g.Key).Intersect(targetHashes.Select(g => g.Key)))
                    {
                        var a = sourceHashes[hash].First();
                        var b = targetHashes[hash].First();
                        if (!string.Equals(a.Title, b.Title, StringComparison.Ordinal) || !string.Equals(a.Ruleset, b.Ruleset, StringComparison.Ordinal))
                            conflicted.Add(toDiffItem(b, DiffCategory.Conflicted, true));
                    }

                    break;
            }

            var result = new ScanResult
            {
                SourceOnly = sourceOnly,
                TargetOnly = targetOnly,
                Conflicted = conflicted,
            };

            currentResult = result;
            return result;
        }

        private async Task<RealmSnapshot> ensureLoadedAsync(string realmId, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
        {
            if (loadedSnapshots.TryGetValue(realmId, out var cached))
                return cached;

            return await LoadRealmSnapshotAsync(realmId, progress, cancellationToken).ConfigureAwait(false);
        }

        private void seedRealmFiles(string? searchDirectory)
        {
            string root = string.IsNullOrWhiteSpace(searchDirectory) ? @"C:\Fake\Ez2Lazer" : searchDirectory;

            try
            {
                Directory.CreateDirectory(Path.Combine(root, "data"));
                Directory.CreateDirectory(Path.Combine(root, "files"));
            }
            catch
            {
                // 测试种子目录可能不可写
            }

            addSeed("ez-main", "Ez2Lazer · client.realm", Path.Combine(root, "data", "client.realm"), 51_003, 18_500_000);
            addSeed("ez-debug", "Ez2Lazer · client_master.realm", Path.Combine(root, "data", "client_master.realm"), 51_003, 9_200_000);
            addSeed("official", "osu!lazer · client.realm", Path.Combine(root, "..", "osu", "data", "client.realm"), 51_000, 22_100_000);
            addSeed("official-backup", "osu!lazer · client_backup.realm", Path.Combine(root, "..", "osu", "data", "backups", "client_backup.realm"), 50_000, 21_000_000);
        }

        private void addSeed(string id, string name, string path, int schema, long size)
        {
            realmFiles[id] = new RealmFileEntry
            {
                Id = id,
                DisplayName = name,
                FilePath = path,
                DataDirectory = Path.GetDirectoryName(path),
                SchemaVersion = schema,
                FileSizeBytes = size,
                IsLocked = false,
            };
        }

        private static List<RealmEntityRow> flattenSnapshot(RealmSnapshot snapshot, EntityKindFilter filter)
        {
            var kinds = filter switch
            {
                EntityKindFilter.BeatmapSet => new[] { EntityKind.BeatmapSet },
                EntityKindFilter.Beatmap => new[] { EntityKind.Beatmap },
                EntityKindFilter.Score => new[] { EntityKind.Score },
                _ => new[] { EntityKind.BeatmapSet, EntityKind.Beatmap, EntityKind.Score },
            };

            return snapshot.Groups.Where(g => kinds.Contains(g.EntityKind)).SelectMany(g => g.Rows).ToList();
        }

        private static DiffItem toDiffItem(RealmEntityRow row, DiffCategory category, bool conflicted = false) => new()
        {
            Id = row.Id,
            Category = category,
            EntityKind = row.EntityKind,
            Title = row.Title,
            Artist = row.Artist,
            Hash = row.Hash,
            Ruleset = row.Ruleset,
            Date = row.Date,
            ConflictSummary = conflicted ? row.Extra ?? "字段不一致 (mock)" : null,
        };
    }
}
