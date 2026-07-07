#if HAS_EZ_OSU_GAME
using osu.Game.Database;

namespace osu.Game.EzRealmSync.Realm.Readers
{
    public interface IRealmReaderAdapter
    {
        RealmReaderRoute SupportedRoute { get; }

        RealmAccess Open(string realmFilePath, int pinnedDiskSchemaVersion);
    }
}
#endif
