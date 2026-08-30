using System.Text.Json;

namespace osu.Game.EzRealmSync.Realm.Readers
{
    public static class RealmReaderPackageCatalog
    {
        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
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
                        DiskSchemaVersions = manifest.DiskSchemaVersions,
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

        public static IReadOnlyList<string> FindDuplicateSchemaWarnings(IEnumerable<RealmReaderPackageInfo> packages)
        {
            var warnings = new List<string>();

            foreach (var group in packages.SelectMany(p => p.DiskSchemaVersions.Select(v => (Version: v, Package: p)))
                         .GroupBy(x => x.Version)
                         .Where(g => g.Count() > 1))
            {
                string ids = string.Join(", ", group.Select(x => x.Package.Id).OrderBy(id => id, StringComparer.OrdinalIgnoreCase));
                warnings.Add($"schema {group.Key} 被多个 reader 包声明：{ids}（将使用 {group.OrderBy(x => x.Package.Id, StringComparer.OrdinalIgnoreCase).First().Package.Id}）");
            }

            return warnings;
        }

        public static RealmReaderPackageInfo? FindForSchema(IEnumerable<RealmReaderPackageInfo> packages, int diskSchemaVersion) =>
            packages
                .Where(p => p.Supports(diskSchemaVersion) && p.HasValidLib)
                .OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

        public static RealmReaderPackageInfo? FindById(IEnumerable<RealmReaderPackageInfo> packages, string? packageId) =>
            string.IsNullOrWhiteSpace(packageId)
                ? null
                : packages.FirstOrDefault(p => string.Equals(p.Id, packageId, StringComparison.OrdinalIgnoreCase));
    }
}
