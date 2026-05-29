using osu.Game.EzRealmSync.Realm;

namespace osu.Game.EzRealmSync.Models
{
    /// <summary>
    /// 已发现 / 已注册的 Realm 文件表（内存），供数据页与集合比对使用。
    /// </summary>
    public sealed class RealmFileRegistry
    {
        private readonly Dictionary<string, RealmFileEntry> files = new();

        public IReadOnlyList<RealmFileEntry> MergeDiscovered(string? searchDirectory)
        {
            foreach (string path in RealmWorkspacePaths.FindRealmFiles(searchDirectory))
                registerPath(path, schemaVersion: null);

            return List();
        }

        public RealmFileEntry Register(string realmFilePath, Func<string, int?>? readSchemaVersion = null)
        {
            string fullPath = Path.GetFullPath(realmFilePath);

            var existing = files.Values.FirstOrDefault(f => string.Equals(f.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                return existing;

            int? schema = readSchemaVersion?.Invoke(fullPath);
            return registerPath(fullPath, schema);
        }

        public bool TryGet(string realmId, out RealmFileEntry entry) => files.TryGetValue(realmId, out entry!);

        public IReadOnlyList<RealmFileEntry> List() =>
            files.Values.OrderBy(f => f.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();

        private RealmFileEntry registerPath(string fullPath, int? schemaVersion)
        {
            bool isLocked = !RealmSyncPathHelper.TryValidateRealmFileAccessible(fullPath, out _);

            long? size = File.Exists(fullPath) ? new FileInfo(fullPath).Length : null;

            var entry = new RealmFileEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = Path.GetFileName(fullPath),
                FilePath = fullPath,
                DataDirectory = RealmWorkspacePaths.ResolveDataDirectory(fullPath),
                SchemaVersion = schemaVersion,
                FileSizeBytes = size,
                IsLocked = isLocked,
            };

            files[entry.Id] = entry;
            return entry;
        }
    }
}
