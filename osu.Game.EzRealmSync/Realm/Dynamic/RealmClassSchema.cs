using System.Diagnostics.CodeAnalysis;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>动态 schema 里的一个类（含嵌入类）。</summary>
    public sealed class RealmClassSchema
    {
        private readonly Dictionary<string, RealmPropertySchema> properties;

        public RealmClassSchema(string name, bool isEmbedded, IEnumerable<RealmPropertySchema> properties)
        {
            Name = name;
            IsEmbedded = isEmbedded;

            this.properties = properties.ToDictionary(p => p.Name, StringComparer.Ordinal);
            PropertyNames = this.properties.Keys.OrderBy(n => n, StringComparer.Ordinal).ToArray();
            PrimaryKeyProperty = this.properties.Values.FirstOrDefault(p => p.IsPrimaryKey)?.Name;
            Signature = buildSignature();
        }

        public string Name { get; }

        public bool IsEmbedded { get; }

        public string? PrimaryKeyProperty { get; }

        public IReadOnlyList<string> PropertyNames { get; }

        public IEnumerable<RealmPropertySchema> Properties => PropertyNames.Select(n => this.properties[n]);

        public int PropertyCount => properties.Count;

        /// <summary>本类的稳定指纹：列按名排序，因此与 Realm 内部的列顺序无关。</summary>
        public string Signature { get; }

        public bool HasProperty(string propertyName) => properties.ContainsKey(propertyName);

        public RealmPropertySchema? Find(string propertyName) => properties.GetValueOrDefault(propertyName);

        public bool TryFindProperty(string propertyName, [NotNullWhen(true)] out RealmPropertySchema? property) =>
            properties.TryGetValue(propertyName, out property);

        private string buildSignature() =>
            $"{Name}|{(IsEmbedded ? "embedded" : "object")}|{PrimaryKeyProperty ?? "-"}\n  "
            + string.Join("\n  ", Properties.Select(p => p.Describe()));
    }
}
