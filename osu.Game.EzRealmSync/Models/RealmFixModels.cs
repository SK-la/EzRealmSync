namespace osu.Game.EzRealmSync.Models
{
    public enum RealmFixIssueKind
    {
        MissingFile,
        IllegalCharacter,
    }

    public sealed class RealmFixIssue
    {
        public Guid Id { get; init; }

        public RealmFixIssueKind Kind { get; init; }

        public EntityKind EntityKind { get; init; }

        public string FieldName { get; init; } = string.Empty;

        public string CurrentValue { get; init; } = string.Empty;

        public string SuggestedValue { get; init; } = string.Empty;

        public string Detail { get; init; } = string.Empty;

        public string? ExpectedFilePath { get; init; }
    }

    public sealed class RealmFixScanOptions
    {
        public bool ScanMissingFiles { get; init; } = true;

        public bool ScanIllegalCharacters { get; init; } = true;

        public string IllegalCharacterReplacement { get; init; } = "_";

        public IReadOnlyList<char> IllegalCharacters { get; init; } = new[] { ',', ':', ';', '/', '\\' };
    }

    public sealed class RealmFixApplyOptions
    {
        public string IllegalCharacterReplacement { get; init; } = "_";
    }

    public sealed class RealmFixApplyResult
    {
        public int AppliedCount { get; init; }

        public int SkippedCount { get; init; }
    }
}
