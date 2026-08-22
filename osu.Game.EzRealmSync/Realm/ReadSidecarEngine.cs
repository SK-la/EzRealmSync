#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// ReadSidecar Worker 内执行的读取 / 导出逻辑（须在 Install reader lib 之后调用）。
    /// </summary>
    public static class ReadSidecarEngine
    {
        public static RealmReadResult ReadDiffSnapshot(RealmReadJob job)
        {
            try
            {
                using var access = open(job);
                var kinds = parseEntityKinds(job.EntityKinds);
                var snapshot = RealmDiffReader.Read(access, cancellationToken: CancellationToken.None);

                var entities = kinds.Count == 0
                    ? snapshot.Entities
                    : snapshot.EnumerateKinds(kinds).ToArray();

                return new RealmReadResult
                {
                    Success = true,
                    Entities = entities.Select(RealmDiffEntityMapping.ToDto).ToList(),
                };
            }
            catch (Exception ex)
            {
                return new RealmReadResult
                {
                    Success = false,
                    ErrorMessage = ExceptionFormatting.SafeFormat(ex),
                };
            }
        }

        public static RealmApplyExportResult ExportApplyBundle(RealmApplyExportJob job)
        {
            try
            {
                using var access = open(new RealmReadJob
                {
                    ReaderLibDirectory = job.ReaderLibDirectory,
                    RealmFilePath = job.SourceRealmFilePath,
                    PinnedDiskSchemaVersion = job.PinnedDiskSchemaVersion,
                    Profile = job.Profile,
                });

                var bundle = OfficialConvertJobExporter.ExportPartialByIds(access, job.ItemIds);

                return new RealmApplyExportResult
                {
                    Success = true,
                    Bundle = bundle,
                };
            }
            catch (Exception ex)
            {
                return new RealmApplyExportResult
                {
                    Success = false,
                    ErrorMessage = ExceptionFormatting.SafeFormat(ex),
                };
            }
        }

        private static RealmAccess open(RealmReadJob job)
        {
            bool ez = string.Equals(job.Profile, "ez", StringComparison.OrdinalIgnoreCase);
            return ez
                ? RealmDiffReader.OpenEzRealm(job.RealmFilePath, job.PinnedDiskSchemaVersion)
                : RealmDiffReader.OpenOfficialRealm(job.RealmFilePath, job.PinnedDiskSchemaVersion);
        }

        private static IReadOnlyList<EntityKind> parseEntityKinds(IReadOnlyList<string> kinds)
        {
            if (kinds.Count == 0)
                return Array.Empty<EntityKind>();

            return kinds
                .Select(k => Enum.TryParse<EntityKind>(k, out var parsed) ? parsed : (EntityKind?)null)
                .Where(k => k != null)
                .Select(k => k!.Value)
                .Distinct()
                .ToArray();
        }
    }
}
#endif
