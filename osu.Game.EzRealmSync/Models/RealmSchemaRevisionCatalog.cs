namespace osu.Game.EzRealmSync.Models
{
    /// <summary>
    /// 内置修订分类表（对照上游/ Ez 侧的 migration 记录人工维护）。
    /// 合并上游 / bump Ez 后由维护者更新。只做分类，不参与任何"版本够不够新"的判定。
    /// </summary>
    public static class RealmSchemaRevisionCatalog
    {
        private static readonly Dictionary<int, RealmSchemaRevisionKind> upstream_kinds = new Dictionary<int, RealmSchemaRevisionKind>
        {
            [50] = RealmSchemaRevisionKind.AddColumn,
            [51] = RealmSchemaRevisionKind.AddColumn,
            [52] = RealmSchemaRevisionKind.AddColumn,
        };

        private static readonly Dictionary<int, RealmSchemaRevisionKind> ez_kinds = new Dictionary<int, RealmSchemaRevisionKind>
        {
            [1] = RealmSchemaRevisionKind.DataChange,
            [2] = RealmSchemaRevisionKind.AddColumn,
            [3] = RealmSchemaRevisionKind.Algorithmic,
            [4] = RealmSchemaRevisionKind.Algorithmic,
            [5] = RealmSchemaRevisionKind.DataChange,
            [6] = RealmSchemaRevisionKind.Algorithmic,
            [7] = RealmSchemaRevisionKind.DataChange,
        };

        public static RealmSchemaRevisionKind ClassifyOfficialUpstream(int upstream) =>
            upstream_kinds.GetValueOrDefault(upstream, RealmSchemaRevisionKind.UpstreamBump);

        public static RealmSchemaRevisionKind ClassifyEzRevision(int ezRevision) =>
            ez_kinds.GetValueOrDefault(ezRevision, RealmSchemaRevisionKind.DataChange);
    }
}
