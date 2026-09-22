using System.Diagnostics.CodeAnalysis;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 动态读出的 schema：类 → 列 → {类型 / 可空 / 主键 / 索引 / objectType}。
    ///
    /// 不含磁盘版本号（那由 <see cref="RealmDiskSchemaReader"/> 单独从文件头读）：
    /// 两份快照的 <see cref="Signature"/> 相等即「schema 一模一样」，与版本号无关。
    /// </summary>
    public sealed class RealmSchemaSnapshot
    {
        private readonly Dictionary<string, RealmClassSchema> classes;

        public RealmSchemaSnapshot(IEnumerable<RealmClassSchema> classes)
        {
            this.classes = new Dictionary<string, RealmClassSchema>(StringComparer.Ordinal);

            foreach (var schema in classes)
                this.classes[schema.Name] = schema;

            ClassNames = this.classes.Keys.OrderBy(n => n, StringComparer.Ordinal).ToArray();
            Signature = string.Join("\n", Classes.Select(c => c.Signature));
        }

        public IReadOnlyList<string> ClassNames { get; }

        public int ClassCount => classes.Count;

        /// <summary>全库稳定指纹，用于「写入前后 schema 未变」的直接比对。</summary>
        public string Signature { get; }

        public IEnumerable<RealmClassSchema> Classes => ClassNames.Select(n => classes[n]);

        public bool HasClass(string className) => classes.ContainsKey(className);

        public RealmClassSchema? Find(string className) => classes.GetValueOrDefault(className);

        public bool TryFindClass(string className, [NotNullWhen(true)] out RealmClassSchema? schema) =>
            classes.TryGetValue(className, out schema);

        /// <summary>双向逐列列出与另一份快照的差异；返回空列表表示完全一致。</summary>
        public IReadOnlyList<string> FindDifferences(RealmSchemaSnapshot other)
        {
            ArgumentNullException.ThrowIfNull(other);

            var differences = new List<string>();

            foreach (string name in ClassNames.Union(other.ClassNames, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal))
            {
                if (!HasClass(name))
                {
                    differences.Add($"类 {name}：另一份有、本份没有");
                    continue;
                }

                if (!other.HasClass(name))
                {
                    differences.Add($"类 {name}：本份有、另一份没有");
                    continue;
                }

                RealmClassSchema left = classes[name];
                RealmClassSchema right = other.classes[name];

                if (left.IsEmbedded != right.IsEmbedded)
                    differences.Add($"{name}：embedded {left.IsEmbedded} != {right.IsEmbedded}");

                if (!string.Equals(left.PrimaryKeyProperty, right.PrimaryKeyProperty, StringComparison.Ordinal))
                    differences.Add($"{name}：主键 {left.PrimaryKeyProperty ?? "-"} != {right.PrimaryKeyProperty ?? "-"}");

                foreach (string propertyName in left.PropertyNames.Union(right.PropertyNames, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal))
                {
                    RealmPropertySchema? a = left.Find(propertyName);
                    RealmPropertySchema? b = right.Find(propertyName);

                    if (a == null)
                        differences.Add($"{name}.{propertyName}：另一份有、本份没有");
                    else if (b == null)
                        differences.Add($"{name}.{propertyName}：本份有、另一份没有");
                    else if (a != b)
                        differences.Add($"{name}.{propertyName}：{a.Describe()} != {b.Describe()}");
                }
            }

            return differences;
        }
    }
}
