#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    internal static class RealmAccessOpener
    {
        public static RealmAccess Open(RealmDiskSchemaKind kind, string realmFilePath, int pinnedDiskSchemaVersion) => kind switch
        {
            RealmDiskSchemaKind.EzExtended => RealmDiffReader.OpenEzRealm(realmFilePath, pinnedDiskSchemaVersion),
            RealmDiskSchemaKind.PpyClient => RealmDiffReader.OpenOfficialRealm(realmFilePath, pinnedDiskSchemaVersion),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "无法打开未知 schema 的 Realm。"),
        };
    }
}
#endif
