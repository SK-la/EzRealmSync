// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Framework.Screens;
using osu.Game.Overlays.Settings.Sections.Maintenance;

namespace osu.EzRealmSync.Screens
{
    public partial class EzRealmSyncDirectoryScreen : DirectorySelectScreen
    {
        private readonly Action<DirectoryInfo> onSelection;
        private readonly DirectoryInfo? initialPath;

        public override LocalisableString HeaderText { get; }

        protected override DirectoryInfo? InitialPath => initialPath;

        public EzRealmSyncDirectoryScreen(string header, Action<DirectoryInfo> onSelection, DirectoryInfo? initialPath = null)
        {
            HeaderText = header;
            this.onSelection = onSelection;
            this.initialPath = initialPath;
        }

        protected override void OnSelection(DirectoryInfo directory)
        {
            onSelection(directory);
            this.Exit();
        }
    }
}
