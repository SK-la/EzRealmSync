using System.Reflection;
using Realms;
using Realms.Schema;
using RealmConfiguration = Realms.RealmConfiguration;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.DllVerifier
{
    /// <summary>
    /// 导出官方**存盘后**的完整 schema（类 → 列 → 类型/可空/主键/索引）。
    ///
    /// 做法是在临时目录里按官方模型新建一份空库、关掉，再动态打开把文件里的 schema 读回来：
    /// 这样拿到的是 realm-core 真正理解的形状（含 <c>[MapTo]</c> 改名、嵌入类型、索引、<c>[Required]</c>
    /// 造成的非空约束），而不是反射或模型侧的推导。
    ///
    /// 测试侧可以拿它与产品侧 <c>DynamicOfficialConverter</c> 产物的 schema 逐列比对，
    /// 从而证明"收窄后的产物等于官方 DLL 认得的那份 schema"。
    /// </summary>
    internal static class OfficialSchemaDump
    {
        public static VerificationOutcome Run()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "ezrealm-dllverifier", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            string probePath = Path.Combine(tempRoot, "official-schema-probe.realm");

            try
            {
                // 用官方模型建一份空库，再把**存盘后的** schema 读回来——官方客户端真正写进文件的
                // 就是这一份。模型自己的 schema 会多出反向链接列（那是模型侧的推导，不落盘），
                // 拿它对拍产物会得到假差异。
                ulong declared = (ulong)(OfficialModelCatalog.DeclaredSchemaVersion ?? 1);
                var config = OfficialModelCatalog.CreateConfiguration(probePath, declared, readOnly: false, allowMigration: false);

                using (OfficialModelCatalog.Open(config))
                {
                    // 建库这一步就把模型 schema 落盘了，这里不需要写数据。
                }

                return RunFileSchema(probePath);
            }
            finally
            {
                deleteQuietly(tempRoot);
            }
        }

        /// <summary>
        /// 导出**某份 .realm 文件里实际存在的** schema。动态打开（不给任何模型），所以读到的必定是
        /// 文件自己的定义，不会掺进进程里的模型。
        ///
        /// 与 <see cref="Run"/> 的产物逐列对比，就是「这份文件是不是还带着官方 schema 没有的表/列」。
        /// </summary>
        public static VerificationOutcome RunFileSchema(string realmPath)
        {
            string fullPath = Path.GetFullPath(realmPath);

            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"找不到 Realm 文件：{fullPath}", fullPath);

            var config = new RealmConfiguration(fullPath)
            {
                IsDynamic = true,
                IsReadOnly = true,
                Schema = Array.Empty<Type>(),
                FallbackPipePath = OfficialModelCatalog.pipeDirectory(),
            };

            using RealmInstance realm = OfficialModelCatalog.Open(config);

            return new VerificationOutcome
            {
                Success = true,
                DeclaredSchemaVersion = OfficialModelCatalog.DeclaredSchemaVersion,
                OpenedSchemaVersion = tryReadSchemaVersionFromHandle(realm),
                Classes = dump(realm.Schema),
            };
        }

        private static int? tryReadSchemaVersionFromHandle(RealmInstance realm)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            object? handle = typeof(RealmInstance).GetField("SharedRealmHandle", flags)?.GetValue(realm);
            MethodInfo? method = handle?.GetType().GetMethod("GetSchemaVersion", flags, binder: null, Type.EmptyTypes, modifiers: null);

            return method?.Invoke(handle, null) is ulong version && version <= int.MaxValue ? (int)version : null;
        }

        private static void deleteQuietly(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch
            {
                // 临时目录清理失败不影响验收结果。
            }
        }

        private static List<SchemaClassOutcome> dump(RealmSchema schema)
        {
            var classes = new List<SchemaClassOutcome>(schema.Count);

            foreach (ObjectSchema objectSchema in schema.OrderBy(s => s.Name, StringComparer.Ordinal))
            {
                var properties = new List<SchemaPropertyOutcome>(objectSchema.Count);

                foreach (Property property in objectSchema)
                {
                    properties.Add(new SchemaPropertyOutcome
                    {
                        Name = property.Name,
                        Type = property.Type.ToString(),
                        TypeCode = (int)property.Type,
                        ObjectType = string.IsNullOrEmpty(property.ObjectType) ? null : property.ObjectType,
                        LinkOriginPropertyName = string.IsNullOrEmpty(property.LinkOriginPropertyName) ? null : property.LinkOriginPropertyName,

                        // PropertyType 是位标志：可空是单独的 bit，没有现成的 IsNullable 属性。
                        Nullable = property.Type.HasFlag(PropertyType.Nullable),
                        IsPrimaryKey = property.IsPrimaryKey,
                        IsIndexed = property.IndexType != IndexType.None,
                        IndexTypeCode = (int)property.IndexType,
                    });
                }

                Property primaryKey = objectSchema.FirstOrDefault(p => p.IsPrimaryKey);

                classes.Add(new SchemaClassOutcome
                {
                    Name = objectSchema.Name,
                    PrimaryKey = string.IsNullOrEmpty(primaryKey.Name) ? null : primaryKey.Name,
                    IsEmbedded = objectSchema.BaseType == ObjectSchema.ObjectType.EmbeddedObject,
                    Properties = properties,
                });
            }

            return classes;
        }
    }
}
