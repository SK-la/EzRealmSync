// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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
