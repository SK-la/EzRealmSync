using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 在 <see cref="RealmDiffEngine"/> 的 GUID Diff 结果上应用集合运算筛选。
    /// </summary>
    public static class RealmSetCompareHelper
    {
        public static ScanResult ApplyOperation(ScanResult diff, RealmSetOperation operation) => operation switch
        {
            RealmSetOperation.Intersection => new ScanResult { Conflicted = diff.Conflicted },
            RealmSetOperation.Union => new ScanResult
            {
                SourceOnly = diff.SourceOnly,
                TargetOnly = diff.TargetOnly,
                Conflicted = diff.Conflicted,
            },
            RealmSetOperation.SymmetricDifference => new ScanResult
            {
                SourceOnly = diff.SourceOnly,
                TargetOnly = diff.TargetOnly,
            },
            _ => diff,
        };

        public static IReadOnlyList<EntityKind> ToEntityKinds(EntityKindFilter filter) => filter switch
        {
            EntityKindFilter.BeatmapSet => new[] { EntityKind.BeatmapSet },
            EntityKindFilter.Beatmap => new[] { EntityKind.Beatmap },
            EntityKindFilter.Score => new[] { EntityKind.Score },
            _ => new[] { EntityKind.BeatmapSet, EntityKind.Beatmap, EntityKind.Score },
        };
    }
}
