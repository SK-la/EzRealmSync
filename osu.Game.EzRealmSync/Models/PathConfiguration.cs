namespace osu.Game.EzRealmSync.Models
{
    public sealed class PathConfiguration
    {
        public string EzDataPath { get; set; } = string.Empty;

        public string OfficialDataPath { get; set; } = string.Empty;

        public string EzRealmFile => RealmWorkspacePaths.ResolveClientRealmPath(EzDataPath);

        public string OfficialRealmFile => RealmWorkspacePaths.ResolveClientRealmPath(OfficialDataPath);
    }
}
