namespace osu.Game.EzRealmSync.Models
{
    public sealed class DiffItem
    {
        public Guid Id { get; init; }

        public DiffCategory Category { get; init; }

        public EntityKind EntityKind { get; init; }

        public string Title { get; init; } = string.Empty;

        public string Artist { get; init; } = string.Empty;

        public string Hash { get; init; } = string.Empty;

        public string Ruleset { get; init; } = string.Empty;

        public DateTimeOffset? Date { get; init; }

        public string? ConflictSummary { get; init; }

        public bool CanApplyToTarget => Category != DiffCategory.Conflicted;
    }
}
