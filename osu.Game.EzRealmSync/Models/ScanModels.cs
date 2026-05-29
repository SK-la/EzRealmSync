// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzRealmSync.Models
{
    public sealed class ScanRequest
    {
        public SyncDirection Direction { get; init; }

        public PathConfiguration Paths { get; init; } = new();

        public IReadOnlyList<EntityKind> EntityKinds { get; init; } = Array.Empty<EntityKind>();
    }

    public sealed class ScanProgress
    {
        public double Progress { get; init; }

        public string Message { get; init; } = string.Empty;
    }

    public sealed class ScanResult
    {
        public IReadOnlyList<DiffItem> SourceOnly { get; init; } = Array.Empty<DiffItem>();

        public IReadOnlyList<DiffItem> TargetOnly { get; init; } = Array.Empty<DiffItem>();

        public IReadOnlyList<DiffItem> Conflicted { get; init; } = Array.Empty<DiffItem>();
    }
}
