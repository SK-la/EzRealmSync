using Realms;
using Realms.Schema;

namespace osu.Game.EzRealmSync.Realm.Dynamic
{
    /// <summary>
    /// 动态 schema 里的一列。<see cref="Type"/> 保留原始 flags（可空 / 集合位）：
    /// Realm 不做 CLR 类型校验，"这列到底能装什么"只能从它读出来。
    /// </summary>
    public sealed record RealmPropertySchema(
        string Name,
        PropertyType Type,
        string ObjectType,
        string? LinkOriginPropertyName,
        bool IsPrimaryKey,
        IndexType Index)
    {
        /// <summary>集合标志位（Array / Set / Dictionary）。</summary>
        public const PropertyType CollectionFlags = PropertyType.Array | PropertyType.Set | PropertyType.Dictionary;

        public bool IsNullable => (Type & PropertyType.Nullable) != 0;

        public bool IsCollection => (Type & CollectionFlags) != 0;

        public bool IsDictionary => (Type & PropertyType.Dictionary) != 0;

        public bool IsSet => (Type & PropertyType.Set) != 0;

        /// <summary>去掉可空与集合标志后的元素类型。</summary>
        public PropertyType ElementType => Type & ~(PropertyType.Nullable | CollectionFlags);

        /// <summary>稳定文本形式：用于快照指纹与差异输出，不用于展示。</summary>
        public string Describe() =>
            $"{Name}:{DescribeType()}"
            + (string.IsNullOrEmpty(ObjectType) ? string.Empty : $"->{ObjectType}")
            + (Index == IndexType.None ? string.Empty : $" idx={Index}")
            + (IsPrimaryKey ? " pk" : string.Empty);

        /// <summary>
        /// 列类型的可读形式。不能直接用 <c>Type.ToString()</c>：<see cref="PropertyType"/> 是 flags 枚举，
        /// 而 <c>Object = 7</c> 与 <c>Bool | String | Date</c> 的位重合，直接打印会得到
        /// "Bool, NullableDouble" 这种假类型名。
        /// </summary>
        public string DescribeType()
        {
            // 集合列的 Nullable 位描述的是元素（IList&lt;string?&gt;），不是集合本身。
            string element = ElementType + (IsNullable ? "?" : string.Empty);

            if (IsDictionary)
                return $"Dictionary<{element}>";

            if (IsSet)
                return $"Set<{element}>";

            return IsCollection ? $"Array<{element}>" : element;
        }
    }
}
