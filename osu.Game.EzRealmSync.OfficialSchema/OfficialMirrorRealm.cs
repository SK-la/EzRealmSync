using osu.Game.EzRealmSync.OfficialSchema.V51;
using Realms;

namespace osu.Game.EzRealmSync.OfficialSchema
{
    /// <summary>镜像 schema pinned 打开/写库；不跑 migration、不触发 Cleanup。</summary>
    public static class OfficialMirrorRealm
    {
        private static readonly Type[] mirror_object_types =
        {
            typeof(RulesetInfo),
            typeof(BeatmapSetInfo),
            typeof(BeatmapInfo),
            typeof(BeatmapMetadata),
            typeof(BeatmapDifficulty),
            typeof(BeatmapUserSettings),
            typeof(RealmUser),
            typeof(ScoreInfo),
            typeof(BeatmapCollection),
            typeof(RealmFile),
            typeof(RealmNamedFileUsage),
            typeof(SkinInfo),
        };

        public static RealmConfiguration CreatePinnedConfiguration(string realmPath, int targetUpstreamSchema) =>
            new RealmConfiguration(realmPath)
            {
                SchemaVersion = (ulong)targetUpstreamSchema,
                MigrationCallback = null,
                Schema = mirror_object_types,
            };

        public static void CreateEmpty(string realmPath, int targetUpstreamSchema)
        {
            string? directory = Path.GetDirectoryName(realmPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (File.Exists(realmPath))
                File.Delete(realmPath);

            using var realm = Realm.GetInstance(CreatePinnedConfiguration(realmPath, targetUpstreamSchema));
            realm.Write(_ => { });
        }

        public static Realm OpenPinned(string realmPath, int targetUpstreamSchema, bool readOnly = false)
        {
            var config = CreatePinnedConfiguration(realmPath, targetUpstreamSchema);
            config.IsReadOnly = readOnly;
            return Realm.GetInstance(config);
        }
    }
}
