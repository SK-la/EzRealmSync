#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    internal static class RealmAccessOpener
    {
        public static RealmAccess Open(RealmDiskSchemaKind kind, string realmFilePath) => kind switch
        {
            RealmDiskSchemaKind.EzExtended => RealmDiffReader.OpenEzRealm(realmFilePath),
            RealmDiskSchemaKind.PpyClient => RealmDiffReader.OpenOfficialRealm(realmFilePath),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "无法打开未知 schema 的 Realm。"),
        };
    }
}
#endif
