using osu.Game.EzRealmSync.OfficialSchema.V51;
using Realms.Schema;

namespace osu.Game.EzRealmSync.OfficialSchema
{
    public static class OfficialMirrorVerifier
    {
        private static readonly string[] forbidden_ez_columns =
        {
            "XxyStarRating",
            "PerformancePoints",
            "HasVideo",
            "HasStoryboard",
            "HostingKind",
            "ExternalContentRoot",
            "HostingKindInt",
            "LastAppliedXxySrVersion",
            "ManiaHitMode",
            "ManiaHealthMode",
        };

        private static readonly string[] forbidden_extra_official_columns =
        {
            "Passed",
        };

        public static (bool Success, string? ErrorMessage, int RealmFileCount) Verify(string realmPath, int targetUpstreamSchema, int sourceFileHashCount)
        {
            try
            {
                using var realm = OfficialMirrorRealm.OpenPinned(realmPath, targetUpstreamSchema, readOnly: true);

                assertNoEzColumns(realm.Schema);
                assertNoExtraOfficialColumns(realm.Schema);

                int fileCount = realm.All<RealmFile>().Count();

                if (sourceFileHashCount > 0 && fileCount < sourceFileHashCount * 0.99)
                {
                    return (false, $"RealmFile 数量异常（期望 ≥ {sourceFileHashCount * 0.99:F0}，实际 {fileCount}）。", fileCount);
                }

                return (true, null, fileCount);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, 0);
            }
        }

        private static void assertNoEzColumns(RealmSchema schema)
        {
            foreach (string objectName in new[] { "Beatmap", "BeatmapSet", "Ruleset", "Score" })
            {
                if (!schema.TryFindObjectSchema(objectName, out ObjectSchema? objectSchema) || objectSchema == null)
                    continue;

                foreach (string forbidden in forbidden_ez_columns)
                {
                    if (objectSchema.TryFindProperty(forbidden, out _))
                        throw new InvalidOperationException($"镜像校验失败：{objectName} 仍含 Ez 列 {forbidden}。");
                }
            }
        }

        private static void assertNoExtraOfficialColumns(RealmSchema schema)
        {
            if (!schema.TryFindObjectSchema("Score", out ObjectSchema? scoreSchema) || scoreSchema == null)
                return;

            foreach (string forbidden in forbidden_extra_official_columns)
            {
                if (scoreSchema.TryFindProperty(forbidden, out _))
                    throw new InvalidOperationException($"镜像校验失败：Score 多写了官方不存在的列 {forbidden}。");
            }
        }
    }
}
