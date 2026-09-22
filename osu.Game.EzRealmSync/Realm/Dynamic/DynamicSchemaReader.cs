using Realms.Schema;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 从**已打开的**动态 Realm 读出完整 schema（类 / 列 / 类型 / 可空 / 主键 / 索引）。
    /// 动态打开不提供 schema，因此读到的必定是文件里既有的定义，不会掺入进程内的模型。
    /// </summary>
    public static class DynamicSchemaReader
    {
        public static RealmSchemaSnapshot Read(DynamicRealmSession session)
        {
            ArgumentNullException.ThrowIfNull(session);
            return Read(session.Realm);
        }

        public static RealmSchemaSnapshot Read(RealmInstance realm)
        {
            ArgumentNullException.ThrowIfNull(realm);

            var classes = new List<RealmClassSchema>(realm.Schema.Count);

            foreach (ObjectSchema objectSchema in realm.Schema)
            {
                var properties = new List<RealmPropertySchema>(objectSchema.Count);

                foreach (Property property in objectSchema)
                {
                    properties.Add(new RealmPropertySchema(
                        property.Name,
                        property.Type,
                        property.ObjectType ?? string.Empty,
                        property.LinkOriginPropertyName,
                        property.IsPrimaryKey,
                        property.IndexType));
                }

                classes.Add(new RealmClassSchema(
                    objectSchema.Name,
                    objectSchema.BaseType == ObjectSchema.ObjectType.EmbeddedObject,
                    properties));
            }

            return new RealmSchemaSnapshot(classes);
        }
    }
}
