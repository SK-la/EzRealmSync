namespace osu.Game.EzRealmSync.Models
{
    /// <summary>从磁盘路径构建 <see cref="RealmFileEntry"/>（不打开 Realm）。</summary>
    public static class RealmFileDiscovery
    {
        public static IReadOnlyList<RealmFileEntry> ListFromSearchDirectory(string? searchDirectory, Func<string, int?>? readSchemaVersion = null)
        {
            var results = new List<RealmFileEntry>();

            foreach (string path in RealmWorkspacePaths.FindRealmFiles(searchDirectory))
            {
                if (TryCreateEntry(path, readSchemaVersion?.Invoke(path), out var entry))
                    results.Add(entry);
            }

            return results.OrderBy(f => f.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static bool TryCreateEntry(string realmFilePath, int? schemaVersion, out RealmFileEntry entry)
        {
            entry = null!;
            string fullPath = Path.GetFullPath(realmFilePath.Trim());

            if (!File.Exists(fullPath))
                return false;

            bool isLocked;

            try
            {
                using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                isLocked = false;
            }
            catch (IOException)
            {
                isLocked = true;
            }

            entry = new RealmFileEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = Path.GetFileName(fullPath),
                FilePath = fullPath,
                DataDirectory = RealmWorkspacePaths.ResolveDataDirectory(fullPath),
                SchemaVersion = schemaVersion,
                FileSizeBytes = new FileInfo(fullPath).Length,
                IsLocked = isLocked,
            };

            return true;
        }
    }
}
