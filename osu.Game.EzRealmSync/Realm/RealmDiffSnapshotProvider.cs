#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm.Readers;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 读取 Diff 快照：优先主 lib 进程内打开；失败时按 manifest 选用 ReadSidecar。
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

            if (tryReadInProcess(realmFilePath, pinnedDiskSchemaVersion, progress, cancellationToken, out RealmDiffSnapshot? snapshot))
                return filterKinds(snapshot, entityKinds);

            return filterKinds(readViaSidecar(realmFilePath, pinnedDiskSchemaVersion, entityKinds, cancellationToken), entityKinds);
        }

        public static RealmAccess OpenOrThrow(string realmFilePath, int pinnedDiskSchemaVersion)
        {
            RealmSchemaToolPolicy.EnsureCanOpen(pinnedDiskSchemaVersion);

            if (tryOpenInProcess(realmFilePath, pinnedDiskSchemaVersion, out RealmAccess? access))
                return access;

            throw createMissingReaderException(pinnedDiskSchemaVersion, realmFilePath);
        }

        public static bool RequiresSidecarForRead(string realmFilePath, int pinnedDiskSchemaVersion) =>
            !tryOpenInProcess(realmFilePath, pinnedDiskSchemaVersion, out _);

        public static RealmSyncApplyBundle ExportApplyBundleViaSidecar(
            string realmFilePath,
            int pinnedDiskSchemaVersion,
            IReadOnlyList<Guid> itemIds,
            CancellationToken cancellationToken = default)
        {
            var package = resolveReaderPackage(pinnedDiskSchemaVersion, realmFilePath);

            var job = new RealmApplyExportJob
            {
                ReaderLibDirectory = package.LibDirectory,
                SourceRealmFilePath = Path.GetFullPath(realmFilePath),
                PinnedDiskSchemaVersion = pinnedDiskSchemaVersion,
                Profile = resolveProfile(pinnedDiskSchemaVersion, package),
                ItemIds = itemIds.ToList(),
            };

            return RealmReadSidecarRunner.ExportApplyBundle(package, job, cancellationToken).Bundle
                   ?? throw new InvalidOperationException("ReadSidecar 未返回 Apply 导出包。");
        }

        private static bool tryReadInProcess(
            string realmFilePath,
            int pinnedDiskSchemaVersion,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken,
            out RealmDiffSnapshot snapshot)
        {
            snapshot = null!;

            if (!tryOpenInProcess(realmFilePath, pinnedDiskSchemaVersion, out RealmAccess? access))
                return false;

            using (access)
            {
                snapshot = RealmDiffReader.Read(access, progress, cancellationToken);
            }

            return true;
        }

        private static bool tryOpenInProcess(string realmFilePath, int pinnedDiskSchemaVersion, out RealmAccess access)
        {
            access = null!;

            try
            {
                access = RealmAccessOpener.Open(realmFilePath, pinnedDiskSchemaVersion);
                access.Run(_ => { });
                return true;
            }
            catch (RealmUserOperationException ex) when (ex.Kind is RealmUserErrorKind.LegacyReaderUnavailable or RealmUserErrorKind.MigrationRequired)
            {
                access?.Dispose();
                return false;
            }
            catch (Exception)
            {
                access?.Dispose();
                return false;
            }
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
                RealmFilePath = Path.GetFullPath(realmFilePath),
                PinnedDiskSchemaVersion = pinnedDiskSchemaVersion,
                Profile = resolveProfile(pinnedDiskSchemaVersion, package),
                EntityKinds = entityKinds?.Select(k => k.ToString()).ToList() ?? new List<string>(),
            };

            var result = RealmReadSidecarRunner.ReadDiffSnapshot(package, job, cancellationToken);
            return RealmDiffEntityMapping.FromResult(result);
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
