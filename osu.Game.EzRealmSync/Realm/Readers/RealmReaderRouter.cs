#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Errors;
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
        private readonly Dictionary<RealmReaderRoute, IRealmReaderAdapter> adapters;

        public RealmReaderRouter()
            : this(null)
        {
        }

        public RealmReaderRouter(IEnumerable<IRealmReaderAdapter>? customAdapters)
        {
            var resolvedAdapters = customAdapters?.ToList() ??
            [
                new OfficialCurrentRealmReaderAdapter(),
                new OfficialLegacyRealmReaderAdapter(),
                new EzCurrentRealmReaderAdapter(),
                new EzLegacyRealmReaderAdapter()
            ];

            adapters = resolvedAdapters.ToDictionary(a => a.SupportedRoute);
        }

        public RealmAccess OpenByRoute(RealmReaderRoute route, string realmFilePath, int pinnedDiskSchemaVersion)
        {
            if (adapters.TryGetValue(route, out IRealmReaderAdapter? adapter))
                return adapter.Open(realmFilePath, pinnedDiskSchemaVersion);

            throw new InvalidOperationException($"未配置 Realm reader 路由：{route}（{realmFilePath}）。");
        }

        public RealmAccess OpenBySchemaKind(RealmDiskSchemaKind kind, string realmFilePath, int pinnedDiskSchemaVersion)
        {
            RealmReaderRoute route = kind switch
            {
                RealmDiskSchemaKind.PpyClient => RealmReaderRoute.OfficialCurrent,
                RealmDiskSchemaKind.EzExtended => RealmReaderRoute.EzCurrent,
                _ => RealmReaderRoute.Unknown,
            };
            return OpenByRoute(route, realmFilePath, pinnedDiskSchemaVersion);
        }

        public RealmAccess OpenByDiskSchemaVersion(int diskSchemaVersion, string realmFilePath)
        {
            RealmReaderRoute route = ResolveRoute(diskSchemaVersion);
            return route switch
            {
                RealmReaderRoute.OfficialCurrent or RealmReaderRoute.OfficialLegacy or
                RealmReaderRoute.EzCurrent or RealmReaderRoute.EzLegacy =>
                    OpenByRoute(route, realmFilePath, diskSchemaVersion),
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

        private sealed class OfficialCurrentRealmReaderAdapter : IRealmReaderAdapter
        {
            public RealmReaderRoute SupportedRoute => RealmReaderRoute.OfficialCurrent;

            public RealmAccess Open(string realmFilePath, int pinnedDiskSchemaVersion) =>
                RealmDiffReader.OpenOfficialRealm(realmFilePath, pinnedDiskSchemaVersion);
        }

        private sealed class OfficialLegacyRealmReaderAdapter : IRealmReaderAdapter
        {
            public RealmReaderRoute SupportedRoute => RealmReaderRoute.OfficialLegacy;

            public RealmAccess Open(string realmFilePath, int pinnedDiskSchemaVersion) => throw createLegacyReaderException(
                realmFilePath,
                pinnedDiskSchemaVersion,
                "official",
                RealmAccess.UpstreamSchemaVersion);
        }

        private sealed class EzCurrentRealmReaderAdapter : IRealmReaderAdapter
        {
            public RealmReaderRoute SupportedRoute => RealmReaderRoute.EzCurrent;

            public RealmAccess Open(string realmFilePath, int pinnedDiskSchemaVersion) =>
                RealmDiffReader.OpenEzRealm(realmFilePath, pinnedDiskSchemaVersion);
        }

        private sealed class EzLegacyRealmReaderAdapter : IRealmReaderAdapter
        {
            public RealmReaderRoute SupportedRoute => RealmReaderRoute.EzLegacy;

            public RealmAccess Open(string realmFilePath, int pinnedDiskSchemaVersion) => throw createLegacyReaderException(
                realmFilePath,
                pinnedDiskSchemaVersion,
                "ez",
                RealmAccess.EzFileSchemaVersion);
        }

        private static RealmUserOperationException createLegacyReaderException(
            string realmFilePath,
            int diskSchemaVersion,
            string profile,
            int currentSupportedVersion)
            => new(
                RealmUserErrorKind.LegacyReaderUnavailable,
                $"检测到 legacy {profile} schema：{diskSchemaVersion}（当前内置支持：{currentSupportedVersion}）。请安装并选择对应版本 reader 后重试。文件：{realmFilePath}");
    }
}
#endif
