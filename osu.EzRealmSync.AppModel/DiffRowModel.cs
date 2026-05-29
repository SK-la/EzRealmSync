using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.AppModel
{
    public sealed class DiffRowModel
    {
        public DiffRowModel(DiffItem item)
        {
            Item = item;
        }

        public DiffItem Item { get; }

        public bool IsSelected { get; set; }

        public string Title => Item.Title;

        public string Artist => Item.Artist;

        public string Hash => Item.Hash;

        public string Ruleset => Item.Ruleset;

        public string Date => Item.Date?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty;
    }
}
