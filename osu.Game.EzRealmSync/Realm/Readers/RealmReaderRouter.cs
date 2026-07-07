#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm.Readers
{
    internal sealed class RealmReaderRouter
    {
        private readonly Dictionary<RealmDiskSchemaKind, IRealmReaderAdapter> adapters;

        public RealmReaderRouter(IEnumerable<IRealmReaderAdapter>? customAdapters = null)
        {
            var resolvedAdapters = customAdapters?.ToList() ?? new List<IRealmReaderAdapter>
            {
                new OfficialRealmReaderAdapter(),
                new EzRealmReaderAdapter(),
            };

            adapters = resolvedAdapters.ToDictionary(a => a.SupportedKind);
        }

        public RealmAccess OpenBySchemaKind(RealmDiskSchemaKind kind, string realmFilePath, int pinnedDiskSchemaVersion)
        {
            if (adapters.TryGetValue(kind, out IRealmReaderAdapter? adapter))
                return adapter.Open(realmFilePath, pinnedDiskSchemaVersion);

            throw new InvalidOperationException($"无法识别或不支持的 Realm schema 类型：{kind}（{realmFilePath}）。");
        }

        public RealmAccess OpenByDiskSchemaVersion(int diskSchemaVersion, string realmFilePath)
        {
            RealmDiskSchemaKind kind = RealmSchemaSafety.Classify(diskSchemaVersion);
            return OpenBySchemaKind(kind, realmFilePath, diskSchemaVersion);
        }

        private sealed class OfficialRealmReaderAdapter : IRealmReaderAdapter
        {
            public RealmDiskSchemaKind SupportedKind => RealmDiskSchemaKind.PpyClient;

            public RealmAccess Open(string realmFilePath, int pinnedDiskSchemaVersion) =>
                RealmDiffReader.OpenOfficialRealm(realmFilePath, pinnedDiskSchemaVersion);
        }

        private sealed class EzRealmReaderAdapter : IRealmReaderAdapter
        {
            public RealmDiskSchemaKind SupportedKind => RealmDiskSchemaKind.EzExtended;

            public RealmAccess Open(string realmFilePath, int pinnedDiskSchemaVersion) =>
                RealmDiffReader.OpenEzRealm(realmFilePath, pinnedDiskSchemaVersion);
        }
    }
}
#endif
