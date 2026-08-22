#if HAS_EZ_OSU_GAME
namespace osu.Game.EzRealmSync.Models
{
    public enum RealmSchemaTransitionRisk
    {
        /// <summary>同 upstream、同 Ez 修订，或仅算法/加列步。</summary>
        Low,

        /// <summary>同 upstream 跨 Ez 修订，或修订分类为 DataChange。</summary>
        Medium,

        /// <summary>upstream 不一致，需 migration 或转官方/升级。</summary>
        High,
    }

    public static class RealmSchemaTransitionAssessor
    {
        public static (int official, int ez) DecodeUpstream(int diskSchemaVersion) =>
            RealmSchemaVersions.Decode(diskSchemaVersion);

        public static bool HasUpstreamMismatch(int schemaA, int schemaB) =>
            DecodeUpstream(schemaA).official != DecodeUpstream(schemaB).official;

        public static RealmSchemaTransitionRisk AssessSyncPair(int schemaA, int schemaB)
        {
            var (offA, ezA) = DecodeUpstream(schemaA);
            var (offB, ezB) = DecodeUpstream(schemaB);

            if (offA != offB)
                return RealmSchemaTransitionRisk.High;

            if (ezA == ezB)
                return RealmSchemaTransitionRisk.Low;

            int minEz = Math.Min(ezA, ezB);
            int maxEz = Math.Max(ezA, ezB);

            for (int ez = minEz + 1; ez <= maxEz; ez++)
            {
                if (RealmSchemaRevisionCatalog.ClassifyEzRevision(ez) is RealmSchemaRevisionKind.DataChange or RealmSchemaRevisionKind.UpstreamBump)
                    return RealmSchemaTransitionRisk.Medium;
            }

            return RealmSchemaTransitionRisk.Low;
        }

        public static string DescribeSyncPairWarning(int schemaA, int schemaB)
        {
            var (offA, ezA) = DecodeUpstream(schemaA);
            var (offB, ezB) = DecodeUpstream(schemaB);

            if (offA != offB)
                return $"A 端官方 upstream {offA} 与 B 端 {offB} 不一致；同步不会改 schema，请确认目标客户端能打开对应版本。";

            if (ezA != ezB)
                return $"A 端 Ez 修订 {ezA} 与 B 端 {ezB} 不一致（同 upstream {offA}）；部分 Ez 字段可能需客户端再处理。";

            return string.Empty;
        }
    }
}
#endif
