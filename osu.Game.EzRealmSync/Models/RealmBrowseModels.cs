namespace osu.Game.EzRealmSync.Models
{
    /// <summary>Realm 数据库中的对象类型（对齐 Realm Studio「Classes」列表）。</summary>
    public enum RealmObjectClass
    {
        Beatmap,
        BeatmapCollection,
        BeatmapMetadata,
        BeatmapSet,
        File,
        Ruleset,
        Score,
        Skin,
    }

    public sealed class RealmColumnDefinition
    {
        public required string Header { get; init; }

        public required string PropertyKey { get; init; }

        public string? TypeHint { get; init; }
    }

    public sealed class RealmBrowseRow
    {
        public Guid Id { get; init; }

        public IReadOnlyDictionary<string, string> Cells { get; init; } = new Dictionary<string, string>();
    }

    public sealed class RealmClassGroup
    {
        public RealmObjectClass Class { get; init; }

        public IReadOnlyList<RealmColumnDefinition> Columns { get; init; } = Array.Empty<RealmColumnDefinition>();

        public IReadOnlyList<RealmBrowseRow> Rows { get; init; } = Array.Empty<RealmBrowseRow>();

        public int Count => Rows.Count;
    }
}
