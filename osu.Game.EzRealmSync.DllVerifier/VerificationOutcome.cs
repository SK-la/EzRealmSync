namespace osu.Game.EzRealmSync.DllVerifier
{
    /// <summary>
    /// 验收结果信封，两个子命令共用。调用方按 <see cref="Classes"/>（dump-schema）与
    /// <see cref="Counts"/>（verify-open）是否为空区分是哪一种。
    /// </summary>
    public sealed class VerificationOutcome
    {
        public bool Success { get; init; }

        public string? Error { get; init; }

        /// <summary>官方包自己声明的 schema 版本；反射读不到时为 null（仅信息，不参与判定）。</summary>
        public int? DeclaredSchemaVersion { get; init; }

        /// <summary>本次打开时实际写进配置的版本。</summary>
        public int? ConfiguredSchemaVersion { get; init; }

        /// <summary>打开后从 native handle 读回的版本。与 <see cref="ConfiguredSchemaVersion"/> 不等就说明官方自己把它迁移了。</summary>
        public int? OpenedSchemaVersion { get; init; }

        /// <summary>读一遍各表的行数（键是 Realm 里的**类名**，如 <c>BeatmapSet</c>／<c>Score</c>）。</summary>
        public Dictionary<string, int>? Counts { get; init; }

        /// <summary>清理软删时各类删掉的行数。</summary>
        public Dictionary<string, int>? CleanedPending { get; init; }

        public List<SchemaClassOutcome>? Classes { get; init; }

        public static VerificationOutcome Failure(string error) => new()
        {
            Success = false,
            Error = error,
        };
    }

    public sealed class SchemaClassOutcome
    {
        public string Name { get; init; } = string.Empty;

        public string? PrimaryKey { get; init; }

        public bool IsEmbedded { get; init; }

        public List<SchemaPropertyOutcome> Properties { get; init; } = new();
    }

    public sealed class SchemaPropertyOutcome
    {
        public string Name { get; init; } = string.Empty;

        public string Type { get; init; } = string.Empty;

        /// <summary><c>Realms.Schema.PropertyType</c> 的数值。调用方与验收器用同一个 Realm 包版本，因此可以安全地强转回去。</summary>
        public int TypeCode { get; init; }

        public string? ObjectType { get; init; }

        /// <summary>backlink 列指回哪个属性；等价于产品侧 <c>LinkOriginPropertyName</c>，比对时必须带上。</summary>
        public string? LinkOriginPropertyName { get; init; }

        public bool Nullable { get; init; }

        public bool IsPrimaryKey { get; init; }

        public bool IsIndexed { get; init; }

        /// <summary><c>Realms.IndexType</c> 的数值。</summary>
        public int IndexTypeCode { get; init; }
    }

    /// <summary>把异常压成一行可读文本：验收器崩在 native 层时消息常常多行且带栈。</summary>
    internal static class ExceptionFormatting
    {
        public static string Describe(Exception ex)
        {
            string message = $"{ex.GetType().Name}: {ex.Message}";

            var inner = ex.InnerException;
            while (inner != null)
            {
                message += $" ← {inner.GetType().Name}: {inner.Message}";
                inner = inner.InnerException;
            }

            return message;
        }
    }
}
