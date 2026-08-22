#if HAS_EZ_OSU_GAME
using System.Security.Cryptography;
using System.Text;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;
using osu.Game.Models;
using osu.Game.Rulesets;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 从已打开的 Realm 构建数据页浏览用 <see cref="RealmSnapshot"/>。
    /// </summary>
    public static class RealmSnapshotBuilder
    {
        public static RealmSnapshot Build(RealmFileEntry file, RealmAccess access, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            RealmSnapshot? snapshot = null;

            access.Run(realm =>
            {
                progress?.Report(new ScanProgress { Progress = 0, Message = "正在读取类型…" });
                cancellationToken.ThrowIfCancellationRequested();

                var classes = new List<RealmClassGroup>
                {
                    readBeatmapSets(realm),
                    readBeatmaps(realm),
                    readMetadata(realm),
                    readScores(realm),
                    readCollections(realm),
                    readFiles(realm),
                    readRulesets(realm),
                    readSkins(realm),
                };

                snapshot = new RealmSnapshot
                {
                    RealmId = file.Id,
                    DisplayName = file.DisplayName,
                    Classes = classes,
                    Groups = RealmSnapshotGrouper.DeriveGroups(classes),
                };
            });

            progress?.Report(new ScanProgress { Progress = 1, Message = "加载完成" });
            return snapshot ?? new RealmSnapshot { RealmId = file.Id, DisplayName = file.DisplayName };
        }

        private static RealmClassGroup readBeatmapSets(RealmInstance realm) => new RealmClassGroup
        {
            Class = RealmObjectClass.BeatmapSet,
            Columns = columns(
                col("Hash", "Hash", "string"),
                col("OnlineID", "Online ID", "int"),
                col("DateAdded", "Date added", "date"),
                col("Status", "Status", "enum")),
            Rows = realm.LiveBeatmapSets().Select(s => row(s.ID, new Dictionary<string, string>
            {
                ["Hash"] = s.Hash,
                ["OnlineID"] = s.OnlineID.ToString(),
                ["DateAdded"] = s.DateAdded.ToString("g"),
                ["Status"] = s.Status.ToString(),
            })).ToList(),
        };

        private static RealmClassGroup readBeatmaps(RealmInstance realm) => new RealmClassGroup
        {
            Class = RealmObjectClass.Beatmap,
            Columns = columns(
                col("Hash", "Hash", "string"),
                col("StarRating", "Stars", "double"),
                col("Ruleset", "Ruleset", "object"),
                col("BeatmapSet", "BeatmapSet", "object")),
            Rows = realm.LiveBeatmaps().Select(b => row(b.ID, new Dictionary<string, string>
            {
                ["Hash"] = b.Hash,
                ["StarRating"] = b.StarRating.ToString("F2"),
                ["Ruleset"] = b.Ruleset?.ShortName ?? string.Empty,
                ["BeatmapSet"] = b.BeatmapSet?.Hash ?? string.Empty,
            })).ToList(),
        };

        private static RealmClassGroup readMetadata(RealmInstance realm)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var rows = new List<RealmBrowseRow>();

            foreach (var beatmap in realm.LiveBeatmaps())
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

            return new RealmClassGroup
            {
                Class = RealmObjectClass.BeatmapMetadata,
                Columns = columns(
                    col("Title", "Title", "string"),
                    col("Artist", "Artist", "string"),
                    col("Source", "Source", "string"),
                    col("Tags", "Tags", "string")),
                Rows = rows,
            };
        }

        private static RealmClassGroup readScores(RealmInstance realm) => new RealmClassGroup
        {
            Class = RealmObjectClass.Score,
            Columns = columns(
                col("Date", "Date", "date"),
                col("TotalScore", "Total score", "int"),
                col("Accuracy", "Accuracy", "double"),
                col("Ruleset", "Ruleset", "object"),
                col("Beatmap", "Beatmap", "object")),
            Rows = realm.LiveScores().Select(s => row(s.ID, new Dictionary<string, string>
            {
                ["Date"] = s.Date.ToString("g"),
                ["TotalScore"] = s.TotalScore.ToString("N0"),
                ["Accuracy"] = s.Accuracy.ToString("P2"),
                ["Ruleset"] = s.Ruleset?.ShortName ?? string.Empty,
                ["Beatmap"] = s.BeatmapInfo?.Hash ?? s.BeatmapHash,
            })).ToList(),
        };

        private static RealmClassGroup readCollections(RealmInstance realm) => new RealmClassGroup
        {
            Class = RealmObjectClass.BeatmapCollection,
            Columns = columns(
                col("Name", "Name", "string"),
                col("BeatmapHashes", "Beatmaps", "list")),
            Rows = realm.All<BeatmapCollection>().AsEnumerable().Select(c => row(c.ID, new Dictionary<string, string>
            {
                ["Name"] = c.Name,
                ["BeatmapHashes"] = c.BeatmapMD5Hashes.Count.ToString(),
            })).ToList(),
        };

        private static RealmClassGroup readFiles(RealmInstance realm) => new RealmClassGroup
        {
            Class = RealmObjectClass.File,
            Columns = columns(
                col("Filename", "Filename", "string"),
                col("Hash", "Hash", "string"),
                col("Present", "Present", "bool")),
            Rows = realm.All<RealmFile>().AsEnumerable().Select(f =>
            {
                var usage = f.Usages.FirstOrDefault();
                return row(GuidFromHash(f.Hash), new Dictionary<string, string>
                {
                    ["Filename"] = usage?.Filename ?? f.Hash,
                    ["Hash"] = f.Hash,
                    ["Present"] = usage != null ? "true" : "false",
                });
            }).ToList(),
        };

        private static RealmClassGroup readRulesets(RealmInstance realm) => new RealmClassGroup
        {
            Class = RealmObjectClass.Ruleset,
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

        private static RealmClassGroup readSkins(RealmInstance realm) => new RealmClassGroup
        {
            Class = RealmObjectClass.Skin,
            Columns = columns(
                col("Name", "Name", "string"),
                col("Creator", "Creator", "string")),
            Rows = realm.LiveSkins().Select(s => row(s.ID, new Dictionary<string, string>
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

        private static RealmBrowseRow row(Guid id, Dictionary<string, string> cells) => new RealmBrowseRow { Id = id, Cells = cells };

        private static IReadOnlyList<RealmColumnDefinition> columns(params RealmColumnDefinition[] defs) => defs;

        private static RealmColumnDefinition col(string key, string title, string typeHint) => new RealmColumnDefinition
        {
            PropertyKey = key,
            Header = title,
            TypeHint = typeHint,
        };
    }
}
#endif
