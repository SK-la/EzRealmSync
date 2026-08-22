#if HAS_EZ_OSU_GAME
using osu.Game.Database;

namespace osu.Game.EzRealmSync.Models
{
    /// <summary>
    /// 内置修订分类表（对照 osu.Game <see cref="RealmAccess"/> migration 注释）。
    /// 合并上游 / bump Ez 后由维护者更新。
    /// </summary>
    public static class RealmSchemaRevisionCatalog
    {
        public const int MinSupportedOfficialUpstream = 50;

        public const int MinSupportedEzRevision = 3;

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

        public static bool IsSupportedOfficialUpstream(int upstream) =>
            upstream >= MinSupportedOfficialUpstream && upstream <= RealmAccess.UpstreamSchemaVersion;

        public static bool IsSupportedEzRevision(int ezRevision) => ezRevision >= MinSupportedEzRevision;

        public static bool IsSupportedDiskSchema(int diskSchemaVersion)
        {
            if (RealmSchemaSafety.IsOfficialDiskSchema(diskSchemaVersion))
                return IsSupportedOfficialUpstream(diskSchemaVersion);

            if (RealmSchemaSafety.IsEzClientDiskSchema(diskSchemaVersion))
            {
                var (official, ez) = RealmSchemaVersions.Decode(diskSchemaVersion);
                return IsSupportedOfficialUpstream(official) && IsSupportedEzRevision(ez);
            }

            return false;
        }
    }
}
#endif
