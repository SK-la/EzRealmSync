using osu.Game.EzRealmSync.Contracts;

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

    /// <summary>「转回官方版」的结果：原路径上的文件已经被收窄成官方 <see cref="TargetSchemaVersion"/>。</summary>
    public sealed class RealmOfficialConversionResult
    {
        public string TargetRealmFilePath { get; init; } = string.Empty;

        public int AppliedCount { get; init; }

        public string? BackupPath { get; init; }

        /// <summary>源库的磁盘版本（Ez 号，如 52010）。</summary>
        public int SourceSchemaVersion { get; init; }

        /// <summary>产物的官方 upstream（文件头写入的版本，如 52）。</summary>
        public int TargetSchemaVersion { get; init; }

        /// <summary>目标 schema 的事实来源（快照或所选的官方库文件）。</summary>
        public string SchemaSourceDescription { get; init; } = string.Empty;

        /// <summary>被剔除的 Ez 表（官方不认识的整表）。</summary>
        public IReadOnlyList<string> DroppedClasses { get; init; } = Array.Empty<string>();

        /// <summary>被剔除的 Ez 列（<c>类.列</c>）。</summary>
        public IReadOnlyList<string> DroppedColumns { get; init; } = Array.Empty<string>();
    }

}
