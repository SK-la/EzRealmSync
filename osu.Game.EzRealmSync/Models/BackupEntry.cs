// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzRealmSync.Models
{
    public sealed class BackupEntry
    {
        public string Id { get; init; } = string.Empty;

        public DateTimeOffset CreatedAt { get; init; }

        public string Description { get; init; } = string.Empty;

        public string Path { get; init; } = string.Empty;
    }
}
