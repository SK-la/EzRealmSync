using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.AppModel
{
    public sealed class RealmBrowseRowModel
    {
        public RealmBrowseRowModel(RealmBrowseRow row, IReadOnlyList<RealmColumnDefinition> columns)
        {
            Id = row.Id;
            foreach (var column in columns)
                values[column.PropertyKey] = row.Cells.TryGetValue(column.PropertyKey, out string? value) ? value : string.Empty;
        }

        private readonly Dictionary<string, string> values = new();

        public Guid Id { get; }

        public string GetCell(string propertyKey) => values.TryGetValue(propertyKey, out string? v) ? v : string.Empty;
    }
}
