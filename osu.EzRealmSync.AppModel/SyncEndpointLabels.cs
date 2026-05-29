using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.AppModel
{
    public static class SyncEndpointLabels
    {
        public static void Get(SyncDirection direction, out string source, out string target)
        {
            if (direction == SyncDirection.EzToOfficial)
            {
                source = "A";
                target = "B";
            }
            else
            {
                source = "B";
                target = "A";
            }
        }
    }
}
