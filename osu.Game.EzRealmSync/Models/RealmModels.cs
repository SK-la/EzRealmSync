namespace osu.Game.EzRealmSync.Models
{
    public sealed class RealmFileEntry
    {
        public required string Id { get; init; }

        public required string DisplayName { get; init; }

        public required string FilePath { get; init; }

        public string? DataDirectory { get; init; }

        public int? SchemaVersion { get; init; }

        public long? FileSizeBytes { get; init; }

        public bool IsLocked { get; init; }

        public string SizeDisplay => FileSizeBytes switch
        {
            null => "—",
            < 1024 => $"{FileSizeBytes} B",
            < 1024 * 1024 => $"{FileSizeBytes / 1024.0:F1} KB",
            _ => $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB",
        };

        public int? OfficialSchemaVersion => SchemaVersion.HasValue ? RealmSchemaVersions.Decode(SchemaVersion).official : null;

        public int? EzRealmSchemaVersion => SchemaVersion.HasValue ? RealmSchemaVersions.Decode(SchemaVersion).ez : null;

        public RealmDiskSchemaKind DiskSchemaKind => RealmSchemaSafety.Classify(SchemaVersion);

        public string OfficialSchemaDisplay => SchemaVersion switch
        {
            null => "—",
            _ when DiskSchemaKind == RealmDiskSchemaKind.PpyClient => SchemaVersion.Value.ToString(),
            _ => OfficialSchemaVersion?.ToString() ?? "—",
        };

        public string EzSchemaDisplay => DiskSchemaKind == RealmDiskSchemaKind.EzExtended && EzRealmSchemaVersion is > 0
            ? EzRealmSchemaVersion.ToString()!
            : "—";

        public string SchemaDisplay => SchemaVersion?.ToString() ?? "—";
    }

    public sealed class RealmEntityRow
    {
        public Guid Id { get; init; }

        public EntityKind EntityKind { get; init; }

        public string Title { get; init; } = string.Empty;

        public string Artist { get; init; } = string.Empty;

        public string Hash { get; init; } = string.Empty;

        public string Ruleset { get; init; } = string.Empty;

        public DateTimeOffset? Date { get; init; }

        public string? Extra { get; init; }
    }

    public sealed class RealmGroupSnapshot
    {
        public EntityKind EntityKind { get; init; }

        public IReadOnlyList<RealmEntityRow> Rows { get; init; } = Array.Empty<RealmEntityRow>();
    }

    public sealed class RealmSnapshot
    {
        public required string RealmId { get; init; }

        public required string DisplayName { get; init; }

        /// <summary>Realm Studio 风格的多类型分组（一级列表 + 二级表格）。</summary>
        public IReadOnlyList<RealmClassGroup> Classes { get; init; } = Array.Empty<RealmClassGroup>();

        /// <summary>同步 Diff 仍使用的简化分组（由 <see cref="Classes"/> 派生）。</summary>
        public IReadOnlyList<RealmGroupSnapshot> Groups { get; init; } = Array.Empty<RealmGroupSnapshot>();

        public int TotalRowCount => Classes.Count > 0 ? Classes.Sum(g => g.Count) : Groups.Sum(g => g.Rows.Count);

        public int ClassCount => Classes.Count > 0 ? Classes.Count : Groups.Count;
    }
}
