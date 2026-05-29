// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Overlays.Dialog;

namespace osu.EzRealmSync.Screens
{
    public partial class EzRealmSyncDeleteDialog : DangerousActionDialog
    {
        public EzRealmSyncDeleteDialog(string message, Action onConfirm)
        {
            HeaderText = message;
            DangerousAction = onConfirm;
        }
    }
}
