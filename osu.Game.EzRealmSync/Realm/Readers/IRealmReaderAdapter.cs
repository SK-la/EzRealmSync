#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm.Readers
{
    internal interface IRealmReaderAdapter
    {
        RealmDiskSchemaKind SupportedKind { get; }

        RealmAccess Open(string realmFilePath, int pinnedDiskSchemaVersion);
    }
}
#endif
