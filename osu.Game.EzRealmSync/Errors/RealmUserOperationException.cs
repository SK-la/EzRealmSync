namespace osu.Game.EzRealmSync.Errors
{
    public enum RealmUserErrorKind
    {
        FileInUse,
        PathConflict,
        /// <summary>对象模型与磁盘 schema 对不上（例如官方库走进了只写 Ez 列的路径）。</summary>
        SchemaModelMismatch,
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
