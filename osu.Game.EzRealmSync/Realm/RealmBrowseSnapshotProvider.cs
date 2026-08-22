#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm.Readers;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 数据 Tab 只读浏览：官方 → Official Worker；Ez current → 进程内；Ez legacy → ReadSidecar。
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

            if (RealmSchemaSafety.IsOfficialDiskSchema(schema))
            {
                EzRealmSyncLog.Info($"ReadBrowseSnapshot via Official Worker schema={schema} file={file.FilePath}");
                return readViaOfficialWorker(file, schema, cancellationToken);
            }

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

        private static RealmSnapshot readViaOfficialWorker(RealmFileEntry file, int pinnedDiskSchemaVersion, CancellationToken cancellationToken)
        {
            string worker = OfficialWriteProcessRunner.ResolveWorkerExecutablePathForTests();
            if (!File.Exists(worker))
            {
                throw new InvalidOperationException(
                    $"无法只读浏览官方 schema {pinnedDiskSchemaVersion}：未找到 Official Worker（{worker}）。请重新 build Desktop 项目。文件：{file.FilePath}");
            }

            var job = new RealmBrowseJob
            {
                ReaderLibDirectory = string.Empty,
                RealmFilePath = Path.GetFullPath(file.FilePath),
                PinnedDiskSchemaVersion = pinnedDiskSchemaVersion,
                Profile = "official",
                RealmId = file.Id,
                DisplayName = file.DisplayName,
            };

            RealmBrowseResult result = OfficialReadProcessRunner.Browse(job, cancellationToken);
            return RealmBrowseSnapshotMapping.FromResult(result);
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
