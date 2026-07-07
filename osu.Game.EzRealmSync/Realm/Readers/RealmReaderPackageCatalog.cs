using System.Text.Json;

namespace osu.Game.EzRealmSync.Realm.Readers
{
    public static class RealmReaderPackageCatalog
    {
        private static readonly JsonSerializerOptions jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static IReadOnlyList<RealmReaderPackageInfo> Scan(string? packagesDirectory = null)
        {
            string root = RealmReaderPaths.ResolvePackagesDirectory(packagesDirectory);
            if (!Directory.Exists(root))
                return Array.Empty<RealmReaderPackageInfo>();

            var packages = new List<RealmReaderPackageInfo>();

            foreach (string packageDirectory in Directory.EnumerateDirectories(root))
            {
                string manifestPath = Path.Combine(packageDirectory, "manifest.json");
                if (!File.Exists(manifestPath))
                    continue;

                try
                {
                    string json = File.ReadAllText(manifestPath);
                    var manifest = JsonSerializer.Deserialize<RealmReaderPackageManifest>(json, jsonOptions);
                    if (manifest == null || string.IsNullOrWhiteSpace(manifest.Id))
                        continue;

                    string libDirectory = Path.GetFullPath(Path.Combine(packageDirectory, string.IsNullOrWhiteSpace(manifest.LibPath) ? "lib" : manifest.LibPath));

                    packages.Add(new RealmReaderPackageInfo
                    {
                        Id = manifest.Id,
                        DisplayName = string.IsNullOrWhiteSpace(manifest.DisplayName) ? manifest.Id : manifest.DisplayName,
                        Profile = manifest.Profile,
                        DiskSchemaVersions = manifest.DiskSchemaVersions ?? Array.Empty<int>(),
                        PackageDirectory = packageDirectory,
                        LibDirectory = libDirectory,
                    });
                }
                catch
                {
                    // 忽略损坏的包目录，避免阻断启动。
                }
            }

            return packages.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public static RealmReaderPackageInfo? FindForSchema(IEnumerable<RealmReaderPackageInfo> packages, int diskSchemaVersion) =>
            packages.FirstOrDefault(p => p.Supports(diskSchemaVersion) && p.HasValidLib);

        public static RealmReaderPackageInfo? FindById(IEnumerable<RealmReaderPackageInfo> packages, string? packageId) =>
            string.IsNullOrWhiteSpace(packageId)
                ? null
                : packages.FirstOrDefault(p => string.Equals(p.Id, packageId, StringComparison.OrdinalIgnoreCase));
    }
}
