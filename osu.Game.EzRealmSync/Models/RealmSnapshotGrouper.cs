namespace osu.Game.EzRealmSync.Models
{
    /// <summary>
    /// 将浏览用 <see cref="RealmClassGroup"/> 转为同步 Diff 用的 <see cref="RealmGroupSnapshot"/>。
    /// </summary>
    public static class RealmSnapshotGrouper
    {
        public static List<RealmGroupSnapshot> DeriveGroups(IReadOnlyList<RealmClassGroup> classes)
        {
            var result = new List<RealmGroupSnapshot>();

            foreach (var (entityKind, objectClass) in new[]
            {
                (EntityKind.BeatmapSet, RealmObjectClass.BeatmapSet),
                (EntityKind.Beatmap, RealmObjectClass.Beatmap),
                (EntityKind.Score, RealmObjectClass.Score),
                (EntityKind.BeatmapCollection, RealmObjectClass.BeatmapCollection),
            })
            {
                var group = classes.FirstOrDefault(c => c.Class == objectClass);
                if (group == null)
                    continue;

                result.Add(new RealmGroupSnapshot
                {
                    EntityKind = entityKind,
                    Rows = group.Rows.Select(r => ToEntityRow(entityKind, r)).ToList(),
                });
            }

            return result;
        }

        public static RealmEntityRow ToEntityRow(EntityKind kind, RealmBrowseRow row)
        {
            string title = row.Cells.TryGetValue("Title", out string? t) ? t
                : row.Cells.TryGetValue("Name", out string? n) ? n
                : row.Cells.TryGetValue("Filename", out string? f) ? f
                : row.Cells.TryGetValue("Hash", out string? h) ? h
                : $"[{kind}] {row.Id:N}";

            if (title.Length > 48)
                title = title[..48];

            return new RealmEntityRow
            {
                Id = row.Id,
                EntityKind = kind,
                Title = title,
                Artist = row.Cells.TryGetValue("Artist", out string? artist) ? artist : string.Empty,
                Hash = row.Cells.TryGetValue("Hash", out string? hash) ? hash : row.Id.ToString("N"),
                Ruleset = row.Cells.TryGetValue("Ruleset", out string? ruleset) ? ruleset
                    : row.Cells.TryGetValue("ShortName", out string? shortName) ? shortName
                    : "osu",
                Date = kind == EntityKind.Score
                    && row.Cells.TryGetValue("Date", out string? date)
                    && DateTimeOffset.TryParse(date, out var parsed)
                        ? parsed
                        : null,
                Extra = kind == EntityKind.BeatmapSet && row.Cells.TryGetValue("OnlineID", out string? onlineId)
                    ? $"OnlineID={onlineId}"
                    : null,
            };
        }
    }
}
