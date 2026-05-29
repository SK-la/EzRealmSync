// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzRealmSync.Models
{
    public enum SyncDirection
    {
        EzToOfficial,
        OfficialToEz,
    }

    public enum EntityKind
    {
        BeatmapSet,
        Beatmap,
        Score,
    }

    public enum DiffCategory
    {
        SourceOnly,
        TargetOnly,
        Conflicted,
    }

    public enum MockDatasetSize
    {
        Empty,
        Medium,
        Large,
    }

    public enum MockErrorInjection
    {
        None,
        ProcessLocked,
        InvalidPath,
        ScanCancelled,
    }
}
