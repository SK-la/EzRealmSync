namespace osu.Game.EzRealmSync.Models
{
    public enum RealmFixIssueKind
    {
        MissingFile,
        IllegalCharacter,
        /// <summary>files/ 中存在但 Realm 未引用的磁盘文件。</summary>
        OrphanFile,
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

        /// <summary>修复目标实体（谱面 / 谱集 / 成绩）的主键。</summary>
        public Guid? TargetEntityId { get; init; }
    }

    public sealed class RealmFixScanOptions
    {
        public bool ScanMissingFiles { get; init; } = true;

        public bool ScanIllegalCharacters { get; init; } = true;

        public bool ScanOrphanFiles { get; init; } = true;

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

    public sealed class RealmOfficialConversionResult
    {
        public string TargetRealmFilePath { get; init; } = string.Empty;

        public int AppliedCount { get; init; }

        public string? BackupPath { get; init; }
    }
}
