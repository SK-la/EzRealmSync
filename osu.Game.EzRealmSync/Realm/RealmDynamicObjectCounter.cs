#if HAS_EZ_OSU_GAME
using Realms;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 不经 typed <see cref="osu.Game.Database.RealmAccess"/>，用动态只读 API 统计磁盘行数（与 schema 探测一致）。
    /// </summary>
    public static class RealmDynamicObjectCounter
    {
        public readonly struct Snapshot
        {
            public int Files { get; init; }

            public int BeatmapSets { get; init; }

            public int Skins { get; init; }

            public int Rulesets { get; init; }

            public int Scores { get; init; }

            public override string ToString() =>
                $"files={Files}, sets={BeatmapSets}, skins={Skins}, rulesets={Rulesets}, scores={Scores}";
        }

        public static Snapshot Capture(string realmFilePath)
        {
            string fullPath = Path.GetFullPath(realmFilePath);
            string tempPathLocation = Path.Combine(Path.GetTempPath(), @"lazer");
            if (!Directory.Exists(tempPathLocation))
                Directory.CreateDirectory(tempPathLocation);

            var config = new RealmConfiguration(fullPath)
            {
                IsDynamic = true,
                IsReadOnly = true,
                FallbackPipePath = tempPathLocation,
            };

            using var realm = RealmInstance.GetInstance(config);

            return new Snapshot
            {
                Files = count(realm, "File"),
                BeatmapSets = count(realm, "BeatmapSet"),
                Skins = count(realm, "Skin"),
                Rulesets = count(realm, "Ruleset"),
                Scores = count(realm, "Score"),
            };
        }

        public static bool TypedReadLooksIncomplete(Snapshot dynamic, RealmMigrationCounts typed)
        {
            if (dynamic.Files >= 1_000 && typed.RealmFiles < dynamic.Files * 0.95)
                return true;

            if (dynamic.BeatmapSets >= 10 && typed.BeatmapSets < dynamic.BeatmapSets * 0.95)
                return true;

            if (dynamic.Skins >= 1 && typed.Skins < dynamic.Skins * 0.95)
                return true;

            if (dynamic.Rulesets >= 4 && typed.Rulesets < dynamic.Rulesets * 0.95)
                return true;

            if (dynamic.Scores >= 100 && typed.Scores < dynamic.Scores * 0.95)
                return true;

            return false;
        }

        private static int count(RealmInstance realm, string className)
        {
            if (!realm.Schema.TryFindObjectSchema(className, out _))
                return 0;

            return realm.DynamicApi.All(className).Count();
        }
    }
}
#endif
