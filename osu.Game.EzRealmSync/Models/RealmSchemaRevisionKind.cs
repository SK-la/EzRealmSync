namespace osu.Game.EzRealmSync.Models
{
    /// <summary>修订步之间的数据风险分类（工具内置，发版时对照 <see cref="osu.Game.Database.RealmAccess"/> migration 维护）。</summary>
    public enum RealmSchemaRevisionKind
    {
        /// <summary>算法可重算 / 默认填充，跨修订同步通常安全。</summary>
        Algorithmic,

        /// <summary>新增可空列或带哨兵默认，Ez→Ez 同步一般安全。</summary>
        AddColumn,

        /// <summary>列语义或默认值变更，同步可能需客户端再处理。</summary>
        DataChange,

        /// <summary>upstream 大版本步进；需 migration，不可假定无损复制。</summary>
        UpstreamBump,
    }
}
