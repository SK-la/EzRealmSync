namespace osu.Game.EzRealmSync.Models
{
    public sealed class RealmCollectionDbImportResult
    {
        public int CollectionCount { get; init; }

        public int CreatedCount { get; init; }

        public int MergedCount { get; init; }

        public int AddedHashCount { get; init; }
    }
}
