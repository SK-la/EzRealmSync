#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm.Readers;

namespace osu.Game.EzRealmSync.Realm
{
    internal static class RealmAccessOpener
    {
        private static readonly RealmReaderRouter reader_router = new RealmReaderRouter();

        public static RealmAccess Open(RealmDiskSchemaKind kind, string realmFilePath, int pinnedDiskSchemaVersion) =>
            reader_router.OpenBySchemaKind(kind, realmFilePath, pinnedDiskSchemaVersion);
    }
}
#endif
