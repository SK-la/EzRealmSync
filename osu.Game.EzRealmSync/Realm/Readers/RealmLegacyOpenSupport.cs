#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm.Readers
{
    internal static class RealmLegacyOpenSupport
    {
        public static RealmAccess OpenOfficialLegacy(string realmFilePath, int pinnedDiskSchemaVersion) =>
            openLegacy(
                () => RealmDiffReader.OpenOfficialRealm(realmFilePath, pinnedDiskSchemaVersion),
                realmFilePath,
                pinnedDiskSchemaVersion,
                "official",
                RealmSchemaToolPolicy.MaxSupportedOfficialSchema);

        public static RealmAccess OpenEzLegacy(string realmFilePath, int pinnedDiskSchemaVersion) =>
            openLegacy(
                () => RealmDiffReader.OpenEzRealm(realmFilePath, pinnedDiskSchemaVersion),
                realmFilePath,
                pinnedDiskSchemaVersion,
                "ez",
                RealmSchemaToolPolicy.MaxSupportedEzFileSchema);

        private static RealmAccess openLegacy(
            Func<RealmAccess> tryPinnedOpen,
            string realmFilePath,
            int pinnedDiskSchemaVersion,
            string profile,
            int currentSupportedVersion)
        {
            try
            {
                return tryPinnedOpen();
            }
            catch (Exception ex) when (RealmOpenErrorClassifier.IsMigrationRequired(ex))
            {
                // 同大版本内旧修订：应走修复页升级，而不是暗示「装 reader 包」
                throw new RealmUserOperationException(
                    RealmUserErrorKind.MigrationRequired,
                    $"当前 osu.Game.dll 无法打开旧版 Realm 文件（{profile}，版本 {pinnedDiskSchemaVersion}；dll 最新：{currentSupportedVersion}）。请先升级 Realm 文件。文件：{realmFilePath}",
                    ex);
            }
            catch (Exception ex)
            {
                throw createLegacyReaderException(realmFilePath, pinnedDiskSchemaVersion, profile, currentSupportedVersion, ex);
            }
        }

        private static RealmUserOperationException createLegacyReaderException(
            string realmFilePath,
            int diskSchemaVersion,
            string profile,
            int currentSupportedVersion,
            Exception? innerException = null)
        {
            return new RealmUserOperationException(
                RealmUserErrorKind.LegacyReaderUnavailable,
                $"缺少对应版本的 osu.Game.dll，无法打开这份 Realm 文件（{profile}，版本 {diskSchemaVersion}；当前 dll 上限：{currentSupportedVersion}）。文件：{realmFilePath}",
                innerException);
        }
    }
}
#endif
