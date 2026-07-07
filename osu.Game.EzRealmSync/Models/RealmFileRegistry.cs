using osu.Game.EzRealmSync.Realm;

namespace osu.Game.EzRealmSync.Models
{
    /// <summary>
    /// 已发现 / 已注册的 Realm 文件表（内存），供数据页与集合比对使用。
    /// </summary>
    public sealed class RealmFileRegistry
    {
        private readonly Dictionary<string, RealmFileEntry> files = new();

        public IReadOnlyList<RealmFileEntry> MergeDiscovered(string? searchDirectory, Func<string, int?>? readSchemaVersion = null)
        {
            foreach (string path in RealmWorkspacePaths.FindRealmFiles(searchDirectory))
                registerPath(path, readSchemaVersion?.Invoke(path));

            return List();
        }

        public RealmFileEntry Register(string realmFilePath, Func<string, int?>? readSchemaVersion = null)
        {
            string fullPath = Path.GetFullPath(realmFilePath);
            int? schema = readSchemaVersion?.Invoke(fullPath);
            return registerPath(fullPath, schema);
        }

        public bool TryGet(string realmId, out RealmFileEntry entry) => files.TryGetValue(realmId, out entry!);

        public IReadOnlyList<RealmFileEntry> List() =>
            files.Values.OrderBy(f => f.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();

        private RealmFileEntry registerPath(string fullPath, int? schemaVersion)
        {
            fullPath = Path.GetFullPath(fullPath);

            var existing = files.Values.FirstOrDefault(f => string.Equals(f.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                var refreshed = createEntry(fullPath, schemaVersion, existing.Id);
                files[existing.Id] = refreshed;
                return refreshed;
            }

            var entry = createEntry(fullPath, schemaVersion);
            files[entry.Id] = entry;
            return entry;
        }

        private static RealmFileEntry createEntry(string fullPath, int? schemaVersion, string? id = null)
        {
            bool isLocked = !RealmSyncPathHelper.TryValidateRealmFileAccessible(fullPath, out _);
            long? size = File.Exists(fullPath) ? new FileInfo(fullPath).Length : null;

            return new RealmFileEntry
            {
                Id = id ?? Guid.NewGuid().ToString("N"),
                DisplayName = Path.GetFileName(fullPath),
                FilePath = fullPath,
                DataDirectory = RealmWorkspacePaths.ResolveDataDirectory(fullPath),
                SchemaVersion = schemaVersion,
                FileSizeBytes = size,
                IsLocked = isLocked,
            };
        }
    }
}
