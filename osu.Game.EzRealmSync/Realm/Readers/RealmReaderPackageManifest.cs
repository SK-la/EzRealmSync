namespace osu.Game.EzRealmSync.Realm.Readers
{
    public sealed class RealmReaderPackageManifest
    {
        public string Id { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        /// <summary>official / ez</summary>
        public string Profile { get; set; } = string.Empty;

        public int[] DiskSchemaVersions { get; set; } = Array.Empty<int>();

        /// <summary>相对包目录的 lib 子路径，默认 lib。</summary>
        public string LibPath { get; set; } = "lib";
    }

    public sealed class RealmReaderPackageInfo
    {
        public required string Id { get; init; }

        public required string DisplayName { get; init; }

        public required string Profile { get; init; }

        public required IReadOnlyList<int> DiskSchemaVersions { get; init; }

        public required string PackageDirectory { get; init; }

        public required string LibDirectory { get; init; }

        public bool Supports(int diskSchemaVersion) => DiskSchemaVersions.Contains(diskSchemaVersion);

        public bool HasValidLib => File.Exists(Path.Combine(LibDirectory, "osu.Game.dll"));
    }
}
