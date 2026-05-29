namespace osu.Game.EzRealmSync.Models
{
    public sealed class PathConfiguration
    {
        public string EzDataPath { get; set; } = string.Empty;

        public string OfficialDataPath { get; set; } = string.Empty;

        /// <summary>直接指定源库文件（优先于 <see cref="EzRealmFile"/> / <see cref="OfficialRealmFile"/> 推导）。</summary>
        public string? SourceRealmFilePath { get; set; }

        /// <summary>直接指定目标库文件。</summary>
        public string? TargetRealmFilePath { get; set; }

        public string EzRealmFile => RealmWorkspacePaths.ResolveClientRealmPath(EzDataPath);

        public string OfficialRealmFile => RealmWorkspacePaths.ResolveClientRealmPath(OfficialDataPath);
    }
}
