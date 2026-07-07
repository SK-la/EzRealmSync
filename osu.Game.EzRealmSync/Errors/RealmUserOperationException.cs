namespace osu.Game.EzRealmSync.Errors
{
    public enum RealmUserErrorKind
    {
        FileInUse,
        MigrationRequired,
        PathConflict,
        LegacyReaderUnavailable,
    }

    public sealed class RealmUserOperationException : InvalidOperationException
    {
        public RealmUserOperationException(RealmUserErrorKind kind, string detail, Exception? innerException = null)
            : base(detail, innerException)
        {
            Kind = kind;
            Detail = detail;
        }

        public RealmUserErrorKind Kind { get; }

        public string Detail { get; }
    }
}
