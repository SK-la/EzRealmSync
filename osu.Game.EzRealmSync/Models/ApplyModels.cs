// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzRealmSync.Models
{
    public sealed class ApplyRequest
    {
        public SyncDirection Direction { get; init; }

        public PathConfiguration Paths { get; init; } = new();

        public IReadOnlyList<Guid> ItemIds { get; init; } = Array.Empty<Guid>();

        public bool CreateBackup { get; init; } = true;

        public bool DeleteFromSource { get; init; }
    }

    public sealed class ApplyProgress
    {
        public double Progress { get; init; }

        public string Message { get; init; } = string.Empty;
    }

    public sealed class ApplyResult
    {
        public int AppliedCount { get; init; }

        public string? BackupPath { get; init; }
    }
}
