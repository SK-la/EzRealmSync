namespace osu.Game.EzRealmSync.Models
{
    /// <summary>磁盘 <c>SchemaVersion</c> 所表示的库种类（与 UI 的 A/B 端槽位无关）。</summary>
    public enum RealmDiskSchemaKind
    {
        Unknown = 0,
        /// <summary>裸 upstream 版本（如 51）。</summary>
        PpyClient,
        /// <summary><c>official * 1000 + ez</c>（如 51006）。</summary>
        EzExtended,
    }
}
