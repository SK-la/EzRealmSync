using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.AppModel
{
    public sealed class BackupEntryRowModel
    {
        public BackupEntryRowModel(BackupEntry entry)
        {
            Entry = entry;
        }

        public BackupEntry Entry { get; }

        public string Id => Entry.Id;

        public string DisplayText => $"{Entry.CreatedAt.LocalDateTime:g} — {Entry.Description}";
    }
}
