namespace osu.Game.EzRealmSync.Models
{
    public sealed class ValidationResult
    {
        public bool IsValid { get; init; }

        public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

        public static ValidationResult Success(params string[] warnings) => new ValidationResult { IsValid = true, Warnings = warnings };

        public static ValidationResult Failure(params string[] errors) => new ValidationResult { IsValid = false, Errors = errors };
    }
}
