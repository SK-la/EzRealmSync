#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm.Readers;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// Diff 快照：官方 → Official Worker；Ez current → 进程内；Ez legacy → ReadSidecar。
    /// </summary>
    public static class RealmDiffSnapshotProvider
    {
        public static RealmDiffSnapshot Read(
            string realmFilePath,
            int pinnedDiskSchemaVersion,
            IReadOnlyList<EntityKind>? entityKinds = null,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            RealmSchemaToolPolicy.EnsureCanOpen(pinnedDiskSchemaVersion);

            if (RealmSchemaSafety.IsOfficialDiskSchema(pinnedDiskSchemaVersion))
            {
                EzRealmSyncLog.Info(
                    $"ReadDiffSnapshot via Official Worker schema={pinnedDiskSchemaVersion} file={realmFilePath}");
                return filterKinds(readViaOfficialWorker(realmFilePath, pinnedDiskSchemaVersion, entityKinds, cancellationToken), entityKinds);
            }

            if (tryReadInProcess(realmFilePath, pinnedDiskSchemaVersion, progress, cancellationToken, out RealmDiffSnapshot snapshot))
                return filterKinds(snapshot, entityKinds);

            if (RealmSchemaToolPolicy.IsAtLatestSupported(pinnedDiskSchemaVersion))
            {
                throw new InvalidOperationException(
                    $"无法用 bundled lib 读取当前 schema {pinnedDiskSchemaVersion} 的 Diff 快照：{realmFilePath}");
            }

            EzRealmSyncLog.Info(
                $"ReadDiffSnapshot via Sidecar schema={pinnedDiskSchemaVersion} file={realmFilePath}");
            return filterKinds(readViaSidecar(realmFilePath, pinnedDiskSchemaVersion, entityKinds, cancellationToken), entityKinds);
        }

        public static RealmAccess OpenOrThrow(string realmFilePath, int pinnedDiskSchemaVersion)
        {
            RealmSchemaToolPolicy.EnsureCanOpen(pinnedDiskSchemaVersion);

            if (RealmSchemaSafety.IsOfficialDiskSchema(pinnedDiskSchemaVersion))
            {
                throw new InvalidOperationException(
                    $"官方 schema {pinnedDiskSchemaVersion} 不得在主进程打开 RealmAccess；请走 Official Worker。文件：{realmFilePath}");
            }

            if (tryOpenInProcess(realmFilePath, pinnedDiskSchemaVersion, out RealmAccess access))
                return access;

            throw createMissingReaderException(pinnedDiskSchemaVersion, realmFilePath);
        }

        public static bool RequiresSidecarForRead(string realmFilePath, int pinnedDiskSchemaVersion) =>
            RealmAccessOpenCore.RequiresOutOfProcessRead(pinnedDiskSchemaVersion);

        public static RealmSyncApplyBundle ExportApplyBundleViaSidecar(
            string realmFilePath,
            int pinnedDiskSchemaVersion,
            IReadOnlyList<Guid> itemIds,
            CancellationToken cancellationToken = default)
        {
            if (RealmSchemaSafety.IsOfficialDiskSchema(pinnedDiskSchemaVersion))
                return exportViaOfficialWorker(realmFilePath, pinnedDiskSchemaVersion, itemIds, cancellationToken);

            var package = resolveReaderPackage(pinnedDiskSchemaVersion, realmFilePath);

            var job = new RealmApplyExportJob
            {
                ReaderLibDirectory = package.LibDirectory,
                SharedLibDirectory = RealmReaderPaths.ResolveSharedLibDirectory(resolveProfile(pinnedDiskSchemaVersion, package)),
                SourceRealmFilePath = Path.GetFullPath(realmFilePath),
                PinnedDiskSchemaVersion = pinnedDiskSchemaVersion,
                Profile = resolveProfile(pinnedDiskSchemaVersion, package),
                ItemIds = itemIds.ToList(),
            };

            return RealmReadSidecarRunner.ExportApplyBundle(package, job, cancellationToken).Bundle
                   ?? throw new InvalidOperationException("ReadSidecar 未返回 Apply 导出包。");
        }

        private static RealmSyncApplyBundle exportViaOfficialWorker(
            string realmFilePath,
            int pinnedDiskSchemaVersion,
            IReadOnlyList<Guid> itemIds,
            CancellationToken cancellationToken)
        {
            ensureOfficialWorkerPresent(pinnedDiskSchemaVersion, realmFilePath);

            var job = new RealmApplyExportJob
            {
                ReaderLibDirectory = string.Empty,
                SourceRealmFilePath = Path.GetFullPath(realmFilePath),
                PinnedDiskSchemaVersion = pinnedDiskSchemaVersion,
                Profile = "official",
                ItemIds = itemIds.ToList(),
            };

            return OfficialReadProcessRunner.ExportApplyBundle(job, cancellationToken).Bundle
                   ?? throw new InvalidOperationException("Official Worker 未返回 Apply 导出包。");
        }

        private static bool tryReadInProcess(
            string realmFilePath,
            int pinnedDiskSchemaVersion,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken,
            out RealmDiffSnapshot snapshot)
        {
            snapshot = null!;

            if (!tryOpenInProcess(realmFilePath, pinnedDiskSchemaVersion, out RealmAccess access))
                return false;

            using (access)
            {
                snapshot = RealmDiffReader.Read(access, progress, cancellationToken);
            }

            return true;
        }

        private static bool tryOpenInProcess(string realmFilePath, int pinnedDiskSchemaVersion, out RealmAccess access)
        {
            if (RealmAccessGateway.TryOpenInProcessForRead(realmFilePath, pinnedDiskSchemaVersion, out RealmAccess? opened) && opened != null)
            {
                access = opened;
                return true;
            }

            access = null!;
            return false;
        }

        private static RealmDiffSnapshot readViaOfficialWorker(
            string realmFilePath,
            int pinnedDiskSchemaVersion,
            IReadOnlyList<EntityKind>? entityKinds,
            CancellationToken cancellationToken)
        {
            ensureOfficialWorkerPresent(pinnedDiskSchemaVersion, realmFilePath);

            var job = new RealmReadJob
            {
                ReaderLibDirectory = string.Empty,
                RealmFilePath = Path.GetFullPath(realmFilePath),
                PinnedDiskSchemaVersion = pinnedDiskSchemaVersion,
                Profile = "official",
                EntityKinds = entityKinds?.Select(k => k.ToString()).ToList() ?? new List<string>(),
            };

            var result = OfficialReadProcessRunner.Read(job, cancellationToken);
            return RealmDiffEntityMapping.FromResult(result);
        }

        private static RealmDiffSnapshot readViaSidecar(
            string realmFilePath,
            int pinnedDiskSchemaVersion,
            IReadOnlyList<EntityKind>? entityKinds,
            CancellationToken cancellationToken)
        {
            var package = resolveReaderPackage(pinnedDiskSchemaVersion, realmFilePath);

            var job = new RealmReadJob
            {
                ReaderLibDirectory = package.LibDirectory,
                SharedLibDirectory = RealmReaderPaths.ResolveSharedLibDirectory(resolveProfile(pinnedDiskSchemaVersion, package)),
                RealmFilePath = Path.GetFullPath(realmFilePath),
                PinnedDiskSchemaVersion = pinnedDiskSchemaVersion,
                Profile = resolveProfile(pinnedDiskSchemaVersion, package),
                EntityKinds = entityKinds?.Select(k => k.ToString()).ToList() ?? new List<string>(),
            };

            var result = RealmReadSidecarRunner.ReadDiffSnapshot(package, job, cancellationToken);
            return RealmDiffEntityMapping.FromResult(result);
        }

        private static void ensureOfficialWorkerPresent(int pinnedDiskSchemaVersion, string realmFilePath)
        {
            string worker = OfficialWriteProcessRunner.ResolveWorkerExecutablePathForTests();
            if (!File.Exists(worker))
            {
                throw new InvalidOperationException(
                    $"无法读取官方 schema {pinnedDiskSchemaVersion}：未找到 Official Worker（{worker}）。请重新 build Desktop 项目。文件：{realmFilePath}");
            }
        }

        private static RealmReaderPackageInfo resolveReaderPackage(int pinnedDiskSchemaVersion, string realmFilePath)
        {
            var package = RealmReaderRegistry.Instance.FindPackageForSchema(pinnedDiskSchemaVersion);
            if (package != null)
                return package;

            throw createMissingReaderException(pinnedDiskSchemaVersion, realmFilePath);
        }

        private static RealmUserOperationException createMissingReaderException(int pinnedDiskSchemaVersion, string realmFilePath) =>
            new RealmUserOperationException(
                RealmUserErrorKind.ReaderPackageMissing,
                $"缺少 schema {pinnedDiskSchemaVersion} 的 reader 包。请在 {RealmReaderRegistry.Instance.PackagesDirectory} 下添加 manifest.json 与 lib/（参见 readers/README.md）。文件：{realmFilePath}");

        private static string resolveProfile(int pinnedDiskSchemaVersion, RealmReaderPackageInfo package)
        {
            if (!string.IsNullOrWhiteSpace(package.Profile))
                return package.Profile;

            return RealmSchemaSafety.IsOfficialDiskSchema(pinnedDiskSchemaVersion) ? "official" : "ez";
        }

        private static RealmDiffSnapshot filterKinds(RealmDiffSnapshot snapshot, IReadOnlyList<EntityKind>? entityKinds)
        {
            if (entityKinds == null || entityKinds.Count == 0)
                return snapshot;

            return new RealmDiffSnapshot
            {
                Entities = snapshot.EnumerateKinds(entityKinds).ToArray(),
            };
        }
    }
}
#endif
