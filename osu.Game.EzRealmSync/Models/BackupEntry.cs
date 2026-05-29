namespace osu.Game.EzRealmSync.Models
{
    public sealed class BackupEntry
    {
        public string Id { get; init; } = string.Empty;

        public DateTimeOffset CreatedAt { get; init; }

        public string Description { get; init; } = string.Empty;

        public string Path { get; init; } = string.Empty;
    }
}
