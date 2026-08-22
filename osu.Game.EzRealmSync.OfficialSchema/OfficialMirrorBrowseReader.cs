using System.Security.Cryptography;
using System.Text;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.OfficialSchema.V51;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.OfficialSchema
{
    /// <summary>官方镜像只读 → 数据 Tab 浏览 DTO（与 ReadSidecar browse 同形）。</summary>
    public static class OfficialMirrorBrowseReader
    {
        public static RealmBrowseResult Read(RealmBrowseJob job)
        {
            try
            {
                using var realm = OfficialMirrorRealm.OpenPinned(job.RealmFilePath, job.PinnedDiskSchemaVersion, readOnly: true);

                return new RealmBrowseResult
                {
                    Success = true,
                    Snapshot = new RealmBrowseSnapshotDto
                    {
                        RealmId = job.RealmId,
                        DisplayName = job.DisplayName,
                        Classes = new List<RealmBrowseClassGroupDto>
                        {
                            readBeatmapSets(realm),
                            readBeatmaps(realm),
                            readMetadata(realm),
                            readScores(realm),
                            readCollections(realm),
                            readFiles(realm),
                            readRulesets(realm),
                            readSkins(realm),
                        },
                    },
                };
            }
            catch (Exception ex)
            {
                return new RealmBrowseResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                };
            }
        }

        private static RealmBrowseClassGroupDto readBeatmapSets(RealmInstance realm) => new RealmBrowseClassGroupDto
        {
            Class = "BeatmapSet",
            Columns = columns(
                col("Hash", "Hash", "string"),
                col("OnlineID", "Online ID", "int"),
                col("DateAdded", "Date added", "date"),
                col("Status", "Status", "enum")),
            Rows = realm.All<BeatmapSetInfo>().AsEnumerable().Where(s => !s.DeletePending).Select(s => row(s.ID, new Dictionary<string, string>
            {
                ["Hash"] = s.Hash,
                ["OnlineID"] = s.OnlineID.ToString(),
                ["DateAdded"] = s.DateAdded.ToString("g"),
                ["Status"] = s.StatusInt.ToString(),
            })).ToList(),
        };

        private static RealmBrowseClassGroupDto readBeatmaps(RealmInstance realm) => new RealmBrowseClassGroupDto
        {
            Class = "Beatmap",
            Columns = columns(
                col("Hash", "Hash", "string"),
                col("StarRating", "Stars", "double"),
                col("Ruleset", "Ruleset", "object"),
                col("BeatmapSet", "BeatmapSet", "object")),
            Rows = realm.All<BeatmapInfo>().AsEnumerable()
                .Where(b => !b.Hidden && b.BeatmapSet != null && !b.BeatmapSet.DeletePending)
                .Select(b => row(b.ID, new Dictionary<string, string>
                {
                    ["Hash"] = b.Hash,
                    ["StarRating"] = b.StarRating.ToString("F2"),
                    ["Ruleset"] = b.Ruleset?.ShortName ?? string.Empty,
                    ["BeatmapSet"] = b.BeatmapSet?.Hash ?? string.Empty,
                })).ToList(),
        };

        private static RealmBrowseClassGroupDto readMetadata(RealmInstance realm)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var rows = new List<RealmBrowseRowDto>();

            foreach (var beatmap in realm.All<BeatmapInfo>().AsEnumerable().Where(b => !b.Hidden))
            {
                var metadata = beatmap.Metadata;
                string key = $"{metadata.Title}\0{metadata.Artist}";

                if (!seen.Add(key))
                    continue;

                rows.Add(row(GuidFromHash(key), new Dictionary<string, string>
                {
                    ["Title"] = metadata.Title,
                    ["Artist"] = metadata.Artist,
                    ["Source"] = metadata.Source,
                    ["Tags"] = metadata.Tags,
                }));
            }

            return new RealmBrowseClassGroupDto
            {
                Class = "BeatmapMetadata",
                Columns = columns(
                    col("Title", "Title", "string"),
                    col("Artist", "Artist", "string"),
                    col("Source", "Source", "string"),
                    col("Tags", "Tags", "string")),
                Rows = rows,
            };
        }

        private static RealmBrowseClassGroupDto readScores(RealmInstance realm) => new RealmBrowseClassGroupDto
        {
            Class = "Score",
            Columns = columns(
                col("Date", "Date", "date"),
                col("TotalScore", "Total score", "int"),
                col("Accuracy", "Accuracy", "double"),
                col("Ruleset", "Ruleset", "object"),
                col("Beatmap", "Beatmap", "object")),
            Rows = realm.All<ScoreInfo>().AsEnumerable().Where(s => !s.DeletePending).Select(s => row(s.ID, new Dictionary<string, string>
            {
                ["Date"] = s.Date.ToString("g"),
                ["TotalScore"] = s.TotalScore.ToString("N0"),
                ["Accuracy"] = s.Accuracy.ToString("P2"),
                ["Ruleset"] = s.Ruleset?.ShortName ?? string.Empty,
                ["Beatmap"] = s.BeatmapInfo?.Hash ?? s.BeatmapHash,
            })).ToList(),
        };

        private static RealmBrowseClassGroupDto readCollections(RealmInstance realm) => new RealmBrowseClassGroupDto
        {
            Class = "BeatmapCollection",
            Columns = columns(
                col("Name", "Name", "string"),
                col("BeatmapHashes", "Beatmaps", "list")),
            Rows = realm.All<BeatmapCollection>().AsEnumerable().Select(c => row(c.ID, new Dictionary<string, string>
            {
                ["Name"] = c.Name,
                ["BeatmapHashes"] = c.BeatmapMD5Hashes.Count.ToString(),
            })).ToList(),
        };

        private static RealmBrowseClassGroupDto readFiles(RealmInstance realm) => new RealmBrowseClassGroupDto
        {
            Class = "File",
            Columns = columns(
                col("Filename", "Filename", "string"),
                col("Hash", "Hash", "string"),
                col("Present", "Present", "bool")),
            Rows = realm.All<RealmFile>().AsEnumerable().Select(f => row(GuidFromHash(f.Hash), new Dictionary<string, string>
            {
                ["Filename"] = f.Hash,
                ["Hash"] = f.Hash,
                ["Present"] = "true",
            })).ToList(),
        };

        private static RealmBrowseClassGroupDto readRulesets(RealmInstance realm) => new RealmBrowseClassGroupDto
        {
            Class = "Ruleset",
            Columns = columns(
                col("ShortName", "Short name", "string"),
                col("Name", "Name", "string"),
                col("Available", "Available", "bool")),
            Rows = realm.All<RulesetInfo>().AsEnumerable().Select(r => row(GuidFromHash(r.ShortName), new Dictionary<string, string>
            {
                ["ShortName"] = r.ShortName,
                ["Name"] = r.Name,
                ["Available"] = string.IsNullOrEmpty(r.InstantiationInfo) ? "false" : "true",
            })).ToList(),
        };

        private static RealmBrowseClassGroupDto readSkins(RealmInstance realm) => new RealmBrowseClassGroupDto
        {
            Class = "Skin",
            Columns = columns(
                col("Name", "Name", "string"),
                col("Creator", "Creator", "string")),
            Rows = realm.All<SkinInfo>().AsEnumerable().Where(s => !s.DeletePending).Select(s => row(s.ID, new Dictionary<string, string>
            {
                ["Name"] = s.Name,
                ["Creator"] = s.Creator,
            })).ToList(),
        };

        private static Guid GuidFromHash(string key)
        {
            byte[] bytes = MD5.HashData(Encoding.UTF8.GetBytes(key));
            return new Guid(bytes);
        }

        private static RealmBrowseRowDto row(Guid id, Dictionary<string, string> cells) =>
            new RealmBrowseRowDto { Id = id, Cells = cells };

        private static List<RealmBrowseColumnDto> columns(params RealmBrowseColumnDto[] defs) => defs.ToList();

        private static RealmBrowseColumnDto col(string key, string title, string typeHint) =>
            new RealmBrowseColumnDto { PropertyKey = key, Header = title, TypeHint = typeHint };
    }
}
