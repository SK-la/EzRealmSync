// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzRealmSync.Models
{
    public sealed class PathConfiguration
    {
        public string EzDataPath { get; set; } = string.Empty;

        public string OfficialDataPath { get; set; } = string.Empty;

        public string EzRealmFile => string.IsNullOrEmpty(EzDataPath) ? string.Empty : Path.Combine(EzDataPath, "client.realm");

        public string OfficialRealmFile => string.IsNullOrEmpty(OfficialDataPath) ? string.Empty : Path.Combine(OfficialDataPath, "client.realm");
    }
}
