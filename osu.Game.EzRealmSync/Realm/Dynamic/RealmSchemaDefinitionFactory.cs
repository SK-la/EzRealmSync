using Realms;
using Realms.Schema;

namespace osu.Game.EzRealmSync.Realm.Dynamic
{
    /// <summary>
    /// 把 <see cref="RealmSchemaSnapshot"/> 还原成 Realm 能接受的 schema 定义。
    ///
    /// 有了它，"官方 N 长什么样"就不再需要 osu.Game 模型：快照本身就是完整定义（含 embedded、
    /// 主键、索引、链接目标、可空与集合位），据此可以新建一份官方格式的空库，再把数据写进去。
    ///
    /// 必须用 <c>Property.PrimitiveList / ObjectList / Backlinks</c> 这类<b>工厂方法</b>还原，不能用
    /// <c>Property</c> 的公开构造函数：构造函数只填可见字段，拿它建出来的 schema 表面上逐列一致，
    /// 但在该上跑值列表读写会抛 <c>KeyNotFoundException（键 ""）</c>——只有工厂方法建出来的列元数据
    /// 是完整的。列类型按快照里<b>原始</b> flags 还原，不做枚举名反推：<c>Object = 7</c> 与若干位组合重名，
    /// 按名字猜会把链接列变成标量列。
    /// </summary>
    public static class RealmSchemaDefinitionFactory
    {
        public static RealmSchema Create(RealmSchemaSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            var builder = new RealmSchema.Builder();

            foreach (ObjectSchema schema in CreateObjectSchemas(snapshot))
                builder.Add(schema);

            return builder.Build();
        }

        public static IEnumerable<ObjectSchema> CreateObjectSchemas(RealmSchemaSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            return snapshot.Classes.Select(createObjectSchema).ToArray();
        }

        private static ObjectSchema createObjectSchema(RealmClassSchema source)
        {
            var builder = new ObjectSchema.Builder(
                source.Name,
                source.IsEmbedded ? ObjectSchema.ObjectType.EmbeddedObject : ObjectSchema.ObjectType.RealmObject);

            foreach (RealmPropertySchema property in source.Properties)
                builder.Add(createProperty(property));

            return builder.Build();
        }

        private static Property createProperty(RealmPropertySchema property)
        {
            // 反向链接列不是"存下来的列"，而是由对方的链接列推出来的：必须有 origin。
            if (property.ElementType == PropertyType.LinkingObjects)
            {
                if (string.IsNullOrEmpty(property.LinkOriginPropertyName))
                    throw new InvalidOperationException($"反向链接列 {property.Name} 缺少来源列名，无法还原。");

                return Property.Backlinks(property.Name, property.ObjectType, property.LinkOriginPropertyName, property.Name);
            }

            if (property.IsCollection)
                return createCollectionProperty(property);

            if (property.ElementType == PropertyType.Object)
                return Property.Object(property.Name, property.ObjectType, property.Name);

            if (property.ElementType == PropertyType.RealmValue)
                return Property.RealmValue(property.Name, property.Name);

            return Property.Primitive(
                property.Name,
                toRealmValueType(property),
                property.IsPrimaryKey,
                property.Index,
                property.IsNullable,
                property.Name);
        }

        private static Property createCollectionProperty(RealmPropertySchema property)
        {
            if (property.ElementType == PropertyType.Object)
            {
                return property switch
                {
                    { IsDictionary: true } => Property.ObjectDictionary(property.Name, property.ObjectType, property.Name),
                    { IsSet: true } => Property.ObjectSet(property.Name, property.ObjectType, property.Name),
                    _ => Property.ObjectList(property.Name, property.ObjectType, property.Name),
                };
            }

            if (property.ElementType == PropertyType.RealmValue)
            {
                if (property is { IsDictionary: true } or { IsSet: true })
                    throw new InvalidOperationException($"暂不支持还原 RealmValue 的集合列 {property.Name}（{property.DescribeType()}）。");

                return Property.RealmValueList(property.Name, property.Name);
            }

            RealmValueType elementType = toRealmValueType(property);

            return property switch
            {
                { IsDictionary: true } => Property.PrimitiveDictionary(property.Name, elementType, property.IsNullable, property.Name),
                { IsSet: true } => Property.PrimitiveSet(property.Name, elementType, property.IsNullable, property.Name),
                _ => Property.PrimitiveList(property.Name, elementType, property.IsNullable, property.Name),
            };
        }

        private static RealmValueType toRealmValueType(RealmPropertySchema property) => property.ElementType switch
        {
            PropertyType.Bool => RealmValueType.Bool,
            PropertyType.Int => RealmValueType.Int,
            PropertyType.Float => RealmValueType.Float,
            PropertyType.Double => RealmValueType.Double,
            PropertyType.String => RealmValueType.String,
            PropertyType.Data => RealmValueType.Data,
            PropertyType.Date => RealmValueType.Date,
            PropertyType.Decimal => RealmValueType.Decimal128,
            PropertyType.ObjectId => RealmValueType.ObjectId,
            PropertyType.Guid => RealmValueType.Guid,
            _ => throw new InvalidOperationException($"列 {property.Name} 的元素类型 {property.ElementType} 无法当作标量还原。"),
        };
    }
}
