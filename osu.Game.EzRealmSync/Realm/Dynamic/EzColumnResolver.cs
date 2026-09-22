namespace osu.Game.EzRealmSync.Realm.Dynamic
{
    /// <summary>
    /// 判定「目标库上哪些列是 Ez 扩展列」——同步时这些列必须保留原值（删行重建不能把 Ez 数据抹掉）。
    ///
    /// 优先用**同版本的官方 schema 快照**：官方 N 没有的列就是 Ez 加列，权威且不需要手维护名单
    /// （<c>Score.Passed</c>、<c>SessionAccuracyCutoffA</c> 这类就没进过静态名单）。没有快照时回退到
    /// 内容判据 <see cref="RealmSchemaSnapshotClassifier.IsEzOnlyProperty"/>。
    ///
    /// 目标是官方库时集合为空：官方 schema 里本来就没有 Ez 列。
    /// </summary>
    public sealed class EzColumnResolver
    {
        private readonly RealmSchemaSnapshot target;
        private readonly RealmSchemaSnapshot? officialBaseline;

        private EzColumnResolver(RealmSchemaSnapshot target, RealmSchemaSnapshot? officialBaseline)
        {
            this.target = target;
            this.officialBaseline = officialBaseline;
        }

        /// <summary>按目标库当前的 schema 与工具目录里该版本的官方快照判定。</summary>
        public static EzColumnResolver Resolve(DynamicRealmSession session)
        {
            ArgumentNullException.ThrowIfNull(session);

            RealmSchemaSnapshot targetSchema = DynamicSchemaReader.Read(session);
            RealmSchemaSnapshot? baseline = RealmSchemaSnapshotStore.Default.TryLoadOfficialBaseline(session.DiskSchemaVersion, out var snapshot, out _)
                ? snapshot
                : null;

            return new EzColumnResolver(targetSchema, baseline);
        }

        public static EzColumnResolver Resolve(RealmSchemaSnapshot targetSchema, RealmSchemaSnapshot? officialBaseline)
        {
            ArgumentNullException.ThrowIfNull(targetSchema);

            return new EzColumnResolver(targetSchema, officialBaseline);
        }

        public bool IsEzColumn(string className, string propertyName)
        {
            if (!target.TryFindClass(className, out RealmClassSchema? schema) || !schema.HasProperty(propertyName))
                return false;

            if (officialBaseline != null && officialBaseline.TryFindClass(className, out RealmClassSchema? official))
                return !official.HasProperty(propertyName);

            return RealmSchemaSnapshotClassifier.IsEzOnlyProperty(className, propertyName);
        }

        /// <summary>该类上全部 Ez 扩展列名。</summary>
        public IReadOnlyList<string> EzColumnsOf(string className)
        {
            if (!target.TryFindClass(className, out RealmClassSchema? schema))
                return Array.Empty<string>();

            return schema.PropertyNames.Where(name => IsEzColumn(className, name)).ToArray();
        }
    }
}
