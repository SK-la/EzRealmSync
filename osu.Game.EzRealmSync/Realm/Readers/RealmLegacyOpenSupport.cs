#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Errors;

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
                RealmAccess.UpstreamSchemaVersion);

        public static RealmAccess OpenEzLegacy(string realmFilePath, int pinnedDiskSchemaVersion) =>
            openLegacy(
                () => RealmDiffReader.OpenEzRealm(realmFilePath, pinnedDiskSchemaVersion),
                realmFilePath,
                pinnedDiskSchemaVersion,
                "ez",
                RealmAccess.EzFileSchemaVersion);

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
                throw;
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
            var installedPackage = RealmReaderRegistry.Instance.FindPackageForSchema(diskSchemaVersion);
            string guidance = installedPackage != null
                ? $"已发现 reader 包「{installedPackage.DisplayName}」（{installedPackage.PackageDirectory}）。请在设置中选择该 reader 并重启应用。"
                : $"可将匹配版本的 osu.Game 依赖放入：{RealmReaderPaths.DefaultPackagesDirectory}\\{{schema}}\\lib\\，并编写 manifest.json。";

            return new RealmUserOperationException(
                RealmUserErrorKind.LegacyReaderUnavailable,
                $"无法用当前内置 reader 打开 legacy {profile} schema：{diskSchemaVersion}（内置支持：{currentSupportedVersion}）。{guidance} 文件：{realmFilePath}",
                innerException);
        }
    }
}
#endif
