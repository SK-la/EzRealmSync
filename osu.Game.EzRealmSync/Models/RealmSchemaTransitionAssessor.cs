namespace osu.Game.EzRealmSync.Models
{
    public static class RealmSchemaTransitionAssessor
    {
        public static (int official, int ez) DecodeUpstream(int diskSchemaVersion) =>
            RealmSchemaVersions.Decode(diskSchemaVersion);

        public static bool HasUpstreamMismatch(int schemaA, int schemaB) =>
            DecodeUpstream(schemaA).official != DecodeUpstream(schemaB).official;

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
