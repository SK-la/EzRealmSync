#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm.Readers
{
    public enum RealmReaderRoute
    {
        Unknown,
        OfficialCurrent,
        OfficialLegacy,
        EzCurrent,
        EzLegacy,
    }

    public sealed class RealmReaderRouter
    {
        private readonly Dictionary<RealmDiskSchemaKind, IRealmReaderAdapter> adapters;

        public RealmReaderRouter()
            : this(null)
        {
        }

        private RealmReaderRouter(IEnumerable<IRealmReaderAdapter>? customAdapters)
        {
            var resolvedAdapters = customAdapters?.ToList() ??
            [
                new OfficialRealmReaderAdapter(),
                new EzRealmReaderAdapter()
            ];

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
            RealmReaderRoute route = ResolveRoute(diskSchemaVersion);
            return route switch
            {
                RealmReaderRoute.OfficialCurrent or RealmReaderRoute.OfficialLegacy =>
                    OpenBySchemaKind(RealmDiskSchemaKind.PpyClient, realmFilePath, diskSchemaVersion),
                RealmReaderRoute.EzCurrent or RealmReaderRoute.EzLegacy =>
                    OpenBySchemaKind(RealmDiskSchemaKind.EzExtended, realmFilePath, diskSchemaVersion),
                _ => throw new InvalidOperationException(
                    $"无法识别或不支持的 Realm schema 版本：{diskSchemaVersion}（{realmFilePath}）。"),
            };
        }

        public RealmReaderRoute ResolveRoute(int diskSchemaVersion)
        {
            RealmDiskSchemaKind kind = RealmSchemaSafety.Classify(diskSchemaVersion);

            if (kind == RealmDiskSchemaKind.Unknown)
                return RealmReaderRoute.Unknown;

            if (kind == RealmDiskSchemaKind.PpyClient)
                return diskSchemaVersion == RealmAccess.UpstreamSchemaVersion
                    ? RealmReaderRoute.OfficialCurrent
                    : RealmReaderRoute.OfficialLegacy;

            if (kind == RealmDiskSchemaKind.EzExtended)
                return diskSchemaVersion == RealmAccess.EzFileSchemaVersion
                    ? RealmReaderRoute.EzCurrent
                    : RealmReaderRoute.EzLegacy;

            return RealmReaderRoute.Unknown;
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
