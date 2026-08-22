#if HAS_EZ_OSU_GAME
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    public static class RealmBrowseSnapshotMapping
    {
        public static RealmBrowseSnapshotDto ToDto(RealmSnapshot snapshot)
        {
            return new RealmBrowseSnapshotDto
            {
                RealmId = snapshot.RealmId,
                DisplayName = snapshot.DisplayName,
                Classes = snapshot.Classes.Select(ToDto).ToList(),
            };
        }

        public static RealmSnapshot FromDto(RealmBrowseSnapshotDto dto)
        {
            var classes = dto.Classes.Select(FromDto).ToArray();
            return new RealmSnapshot
            {
                RealmId = dto.RealmId,
                DisplayName = dto.DisplayName,
                Classes = classes,
                Groups = RealmSnapshotGrouper.DeriveGroups(classes),
            };
        }

        public static RealmSnapshot FromResult(RealmBrowseResult result)
        {
            if (!result.Success || result.Snapshot == null)
                throw new InvalidOperationException(result.ErrorMessage ?? "ReadSidecar 浏览快照失败。");

            return FromDto(result.Snapshot);
        }

        private static RealmBrowseClassGroupDto ToDto(RealmClassGroup group) =>
            new RealmBrowseClassGroupDto
            {
                Class = group.Class.ToString(),
                Columns = group.Columns.Select(c => new RealmBrowseColumnDto
                {
                    Header = c.Header,
                    PropertyKey = c.PropertyKey,
                    TypeHint = c.TypeHint,
                }).ToList(),
                Rows = group.Rows.Select(r => new RealmBrowseRowDto
                {
                    Id = r.Id,
                    Cells = new Dictionary<string, string>(r.Cells),
                }).ToList(),
            };

        private static RealmClassGroup FromDto(RealmBrowseClassGroupDto dto) =>
            new RealmClassGroup
            {
                Class = Enum.Parse<RealmObjectClass>(dto.Class),
                Columns = dto.Columns.Select(c => new RealmColumnDefinition
                {
                    Header = c.Header,
                    PropertyKey = c.PropertyKey,
                    TypeHint = c.TypeHint,
                }).ToArray(),
                Rows = dto.Rows.Select(r => new RealmBrowseRow
                {
                    Id = r.Id,
                    Cells = new Dictionary<string, string>(r.Cells),
                }).ToArray(),
            };
    }
}
#endif
