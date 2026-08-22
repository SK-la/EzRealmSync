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
                    $"无法用当前内置模型 pinned 打开 legacy {profile} schema {pinnedDiskSchemaVersion}（内置最新：{currentSupportedVersion}）。请在「修复」页「升级到最新版」，或对 Ez 库使用「转回官方版」（会自动先升级）。文件：{realmFilePath}",
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
                $"无法打开 legacy {profile} schema：{diskSchemaVersion}（内置支持：{currentSupportedVersion}）。本阶段仅支持同大版本，不提供跨版本 reader DLL。请先将库升到 {currentSupportedVersion} 或更新工具。文件：{realmFilePath}",
                innerException);
        }
    }
}
#endif
