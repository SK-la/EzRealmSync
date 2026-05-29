namespace osu.Game.EzRealmSync.Models
{
    public sealed class ValidationResult
    {
        public bool IsValid { get; init; }

        public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

        public static ValidationResult Success() => new() { IsValid = true };

        public static ValidationResult Failure(params string[] errors) => new() { IsValid = false, Errors = errors };
    }
}
