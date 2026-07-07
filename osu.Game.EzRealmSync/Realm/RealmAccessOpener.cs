#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Realm.Readers;

namespace osu.Game.EzRealmSync.Realm
{
    internal static class RealmAccessOpener
    {
        public static RealmAccess Open(string realmFilePath, int pinnedDiskSchemaVersion) =>
            RealmReaderRegistry.Instance.Router.OpenByDiskSchemaVersion(pinnedDiskSchemaVersion, realmFilePath);
    }
}
#endif
