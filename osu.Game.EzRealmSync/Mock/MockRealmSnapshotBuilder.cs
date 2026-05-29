using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Mock
{
    public static class MockRealmSnapshotBuilder
    {
        public static RealmSnapshot Build(RealmFileEntry file, MockDatasetSize size)
        {
            int scale = size switch
            {
                MockDatasetSize.Empty => 0,
                MockDatasetSize.Large => 5,
                _ => 1,
            };

            var classes = new List<RealmClassGroup>
            {
                buildClass(RealmObjectClass.BeatmapSet, scale, 8),
                buildClass(RealmObjectClass.Beatmap, scale, 24),
                buildClass(RealmObjectClass.BeatmapMetadata, scale, 8),
                buildClass(RealmObjectClass.Score, scale, 40),
                buildClass(RealmObjectClass.BeatmapCollection, scale, 3),
                buildClass(RealmObjectClass.File, scale, 16),
                buildClass(RealmObjectClass.Ruleset, Math.Max(scale, 1), 4),
                buildClass(RealmObjectClass.Skin, scale, 2),
            };

            return new RealmSnapshot
            {
                RealmId = file.Id,
                DisplayName = file.DisplayName,
                Classes = classes,
                Groups = deriveGroups(classes),
            };
        }

        private static RealmClassGroup buildClass(RealmObjectClass @class, int scale, int baseCount)
        {
            var columns = columnsFor(@class);
            int count = baseCount * scale;
            var rows = new List<RealmBrowseRow>(count);

            for (int i = 0; i < count; i++)
                rows.Add(generateRow(@class, columns, i));

            return new RealmClassGroup
            {
                Class = @class,
                Columns = columns,
                Rows = rows,
            };
        }

        private static IReadOnlyList<RealmColumnDefinition> columnsFor(RealmObjectClass @class) => @class switch
        {
            RealmObjectClass.BeatmapSet => new[]
            {
                col("Hash", "Hash", "string"),
                col("OnlineID", "Online ID", "int"),
                col("DateAdded", "Date added", "date"),
                col("Status", "Status", "enum"),
            },
            RealmObjectClass.Beatmap => new[]
            {
                col("Hash", "Hash", "string"),
                col("StarRating", "Stars", "double"),
                col("Ruleset", "Ruleset", "object"),
                col("BeatmapSet", "BeatmapSet", "object"),
            },
            RealmObjectClass.BeatmapMetadata => new[]
            {
                col("Title", "Title", "string"),
                col("Artist", "Artist", "string"),
                col("Source", "Source", "string"),
                col("Tags", "Tags", "string"),
            },
            RealmObjectClass.Score => new[]
            {
                col("Date", "Date", "date"),
                col("TotalScore", "Total score", "int"),
                col("Accuracy", "Accuracy", "double"),
                col("Ruleset", "Ruleset", "object"),
                col("Beatmap", "Beatmap", "object"),
            },
            RealmObjectClass.BeatmapCollection => new[]
            {
                col("Name", "Name", "string"),
                col("BeatmapHashes", "Beatmaps", "list"),
            },
            RealmObjectClass.File => new[]
            {
                col("Filename", "Filename", "string"),
                col("Hash", "Hash", "string"),
                col("Present", "Present", "bool"),
            },
            RealmObjectClass.Ruleset => new[]
            {
                col("ShortName", "Short name", "string"),
                col("Name", "Name", "string"),
                col("Available", "Available", "bool"),
            },
            RealmObjectClass.Skin => new[]
            {
                col("Name", "Name", "string"),
                col("Creator", "Creator", "string"),
            },
            _ => Array.Empty<RealmColumnDefinition>(),
        };

        private static RealmColumnDefinition col(string key, string title, string typeHint) => new()
        {
            PropertyKey = key,
            Header = title,
            TypeHint = typeHint,
        };

        private static RealmBrowseRow generateRow(RealmObjectClass @class, IReadOnlyList<RealmColumnDefinition> columns, int index)
        {
            var cells = new Dictionary<string, string>();

            foreach (var column in columns)
                cells[column.PropertyKey] = cellValue(@class, column.PropertyKey, index);

            return new RealmBrowseRow
            {
                Id = Guid.NewGuid(),
                Cells = cells,
            };
        }

        private static string cellValue(RealmObjectClass @class, string key, int index)
        {
            string hash = Convert.ToHexString(Guid.NewGuid().ToByteArray())[..32].ToLowerInvariant();

            return (@class, key) switch
            {
                (RealmObjectClass.BeatmapSet, "Hash") => hash,
                (RealmObjectClass.BeatmapSet, "OnlineID") => (1000 + index).ToString(),
                (RealmObjectClass.BeatmapSet, "DateAdded") => DateTimeOffset.UtcNow.AddDays(-index).ToString("g"),
                (RealmObjectClass.BeatmapSet, "Status") => index % 3 == 0 ? "LocallyModified" : "Ranked",

                (RealmObjectClass.Beatmap, "Hash") => hash,
                (RealmObjectClass.Beatmap, "StarRating") => (4.5 + index % 5 * 0.3).ToString("F2"),
                (RealmObjectClass.Beatmap, "Ruleset") => rulesetName(index),
                (RealmObjectClass.Beatmap, "BeatmapSet") => $"BeatmapSet({1000 + index % 8})",

                (RealmObjectClass.BeatmapMetadata, "Title") => $"Mock Title #{index + 1}",
                (RealmObjectClass.BeatmapMetadata, "Artist") => "Mock Artist",
                (RealmObjectClass.BeatmapMetadata, "Source") => index % 2 == 0 ? "osu!" : string.Empty,
                (RealmObjectClass.BeatmapMetadata, "Tags") => index % 3 == 0 ? "mock,test" : string.Empty,

                (RealmObjectClass.Score, "Date") => DateTimeOffset.UtcNow.AddDays(-index).ToString("g"),
                (RealmObjectClass.Score, "TotalScore") => (1_000_000 - index * 137).ToString("N0"),
                (RealmObjectClass.Score, "Accuracy") => (0.99 - index * 0.001).ToString("P2"),
                (RealmObjectClass.Score, "Ruleset") => rulesetName(index),
                (RealmObjectClass.Score, "Beatmap") => $"Beatmap({hash[..8]}…)",

                (RealmObjectClass.BeatmapCollection, "Name") => $"Collection {index + 1}",
                (RealmObjectClass.BeatmapCollection, "BeatmapHashes") => (12 + index).ToString(),

                (RealmObjectClass.File, "Filename") => $"mock-file-{index}.osu",
                (RealmObjectClass.File, "Hash") => hash,
                (RealmObjectClass.File, "Present") => index % 5 == 0 ? "false" : "true",

                (RealmObjectClass.Ruleset, "ShortName") => rulesetName(index),
                (RealmObjectClass.Ruleset, "Name") => rulesetName(index) switch
                {
                    "osu" => "osu!",
                    "mania" => "osu!mania",
                    "taiko" => "osu!taiko",
                    _ => "osu!catch",
                },
                (RealmObjectClass.Ruleset, "Available") => "true",

                (RealmObjectClass.Skin, "Name") => $"Skin {index + 1}",
                (RealmObjectClass.Skin, "Creator") => "Mock Creator",

                _ => string.Empty,
            };
        }

        private static string rulesetName(int index) => (index % 4) switch
        {
            0 => "osu",
            1 => "mania",
            2 => "taiko",
            _ => "catch",
        };

        private static List<RealmGroupSnapshot> deriveGroups(IReadOnlyList<RealmClassGroup> classes)
        {
            var result = new List<RealmGroupSnapshot>();

            foreach (var (entityKind, objectClass) in new[]
            {
                (EntityKind.BeatmapSet, RealmObjectClass.BeatmapSet),
                (EntityKind.Beatmap, RealmObjectClass.Beatmap),
                (EntityKind.Score, RealmObjectClass.Score),
            })
            {
                var group = classes.FirstOrDefault(c => c.Class == objectClass);
                if (group == null)
                    continue;

                result.Add(new RealmGroupSnapshot
                {
                    EntityKind = entityKind,
                    Rows = group.Rows.Select(r => toEntityRow(entityKind, r)).ToList(),
                });
            }

            return result;
        }

        private static RealmEntityRow toEntityRow(EntityKind kind, RealmBrowseRow row)
        {
            string title = row.Cells.TryGetValue("Title", out string? t) ? t
                : row.Cells.TryGetValue("Name", out string? n) ? n
                : row.Cells.TryGetValue("Filename", out string? f) ? f
                : $"[{kind}] {row.Id:N}".Substring(0, Math.Min(48, $"[{kind}] {row.Id:N}".Length));

            return new RealmEntityRow
            {
                Id = row.Id,
                EntityKind = kind,
                Title = title,
                Artist = row.Cells.TryGetValue("Artist", out string? artist) ? artist : "Mock Artist",
                Hash = row.Cells.TryGetValue("Hash", out string? hash) ? hash : row.Id.ToString("N"),
                Ruleset = row.Cells.TryGetValue("Ruleset", out string? ruleset) ? ruleset : row.Cells.TryGetValue("ShortName", out string? sn) ? sn : "osu",
                Date = kind == EntityKind.Score && row.Cells.TryGetValue("Date", out string? date) && DateTimeOffset.TryParse(date, out var parsed)
                    ? parsed
                    : null,
                Extra = kind == EntityKind.BeatmapSet && row.Cells.TryGetValue("OnlineID", out string? onlineId) ? $"OnlineID={onlineId}" : null,
            };
        }
    }
}
