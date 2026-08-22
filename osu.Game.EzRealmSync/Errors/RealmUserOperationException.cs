namespace osu.Game.EzRealmSync.Errors
{
    public enum RealmUserErrorKind
    {
        FileInUse,
        MigrationRequired,
        PathConflict,
        LegacyReaderUnavailable,
        /// <summary>磁盘 schema 低于本工具同大版本最低支持。</summary>
        SchemaTooLow,
        /// <summary>磁盘 schema 高于本工具内置 lib。</summary>
        SchemaTooHigh,
        /// <summary>版本号已是最新但对象模型仍无法 pinned 打开（工具/游戏脱节或脏库）。</summary>
        SchemaModelMismatch,
        /// <summary>磁盘 schema 无匹配 reader 包（readers/ manifest + lib）。</summary>
        ReaderPackageMissing,
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
