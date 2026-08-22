#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm.Readers;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 数据 Tab 只读浏览快照：current schema 进程内；legacy schema ReadSidecar + reader 包。
    /// </summary>
    public static class RealmBrowseSnapshotProvider
    {
        public static RealmSnapshot Read(
            RealmFileEntry file,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            int schema = RealmAccessGateway.ProbeSchema(file.FilePath)
                         ?? throw new InvalidOperationException($"无法读取 Realm schema 版本：{file.FilePath}");

            RealmSchemaToolPolicy.EnsureCanOpen(schema);

            if (tryReadInProcess(file, schema, progress, cancellationToken, out RealmSnapshot snapshot))
                return snapshot;

            if (RealmSchemaToolPolicy.IsAtLatestSupported(schema))
            {
                throw new InvalidOperationException(
                    $"无法用 bundled lib 只读浏览当前 schema {schema}：{file.FilePath}");
            }

            EzRealmSyncLog.Info($"ReadBrowseSnapshot via Sidecar schema={schema} file={file.FilePath}");

            return readViaSidecar(file, schema, cancellationToken);
        }

        private static bool tryReadInProcess(
            RealmFileEntry file,
            int pinnedDiskSchemaVersion,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken,
            out RealmSnapshot snapshot)
        {
            snapshot = null!;

            if (!RealmAccessGateway.TryOpenInProcessForRead(file.FilePath, pinnedDiskSchemaVersion, out RealmAccess? access) || access == null)
                return false;

            using (access)
                snapshot = RealmSnapshotBuilder.Build(file, access, progress, cancellationToken);

            return true;
        }

        private static RealmSnapshot readViaSidecar(RealmFileEntry file, int pinnedDiskSchemaVersion, CancellationToken cancellationToken)
        {
            RealmReaderPackageInfo package = RealmReaderRegistry.Instance.FindPackageForSchema(pinnedDiskSchemaVersion)
                                             ?? throw createMissingReaderException(pinnedDiskSchemaVersion, file.FilePath);

            string profile = resolveProfile(pinnedDiskSchemaVersion, package);

            var job = new RealmBrowseJob
            {
                ReaderLibDirectory = package.LibDirectory,
                SharedLibDirectory = RealmReaderPaths.ResolveSharedLibDirectory(profile),
                RealmFilePath = Path.GetFullPath(file.FilePath),
                PinnedDiskSchemaVersion = pinnedDiskSchemaVersion,
                Profile = profile,
                RealmId = file.Id,
                DisplayName = file.DisplayName,
            };

            RealmBrowseResult result = RealmReadSidecarRunner.ReadBrowseSnapshot(package, job, cancellationToken);

            return RealmBrowseSnapshotMapping.FromResult(result);
        }

        private static RealmUserOperationException createMissingReaderException(int pinnedDiskSchemaVersion, string realmFilePath)
        {
            return new RealmUserOperationException(
                RealmUserErrorKind.ReaderPackageMissing,
                $"无法只读浏览 schema {pinnedDiskSchemaVersion}：缺少 reader 包。请在 {RealmReaderRegistry.Instance.PackagesDirectory} 下添加 manifest.json 与 lib/（参见 readers/README.md），并运行 Sync-ReaderLibs.ps1。文件：{realmFilePath}");
        }

        private static string resolveProfile(int pinnedDiskSchemaVersion, RealmReaderPackageInfo package)
        {
            if (!string.IsNullOrWhiteSpace(package.Profile))
                return package.Profile;

            return RealmSchemaSafety.IsOfficialDiskSchema(pinnedDiskSchemaVersion) ? "official" : "ez";
        }
    }
}
#endif
