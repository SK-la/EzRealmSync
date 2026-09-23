using System.Reflection;
using osu.Game.Beatmaps;
using Realms;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.DllVerifier
{
    /// <summary>
    /// 官方模型与打开配置。类型清单由 <c>ppy.osu.Game</c> 里 Fody 织入的模型**反射枚举**得来，
    /// 不手抄：官方加表加列时这个验收器自动跟上，不会像手写镜像那样悄悄落后。
    /// </summary>
    internal static class OfficialModelCatalog
    {
        private static readonly Lazy<Type[]> object_types = new(discoverObjectTypes, isThreadSafe: true);

        private static readonly Lazy<int?> declared_schema_version = new(tryReadDeclaredSchemaVersion, isThreadSafe: true);

        public static Type[] ObjectTypes => object_types.Value;

        /// <summary>官方包声明的 schema 版本（官方客户端会写进文件头的号）；读不到时为 null。</summary>
        public static int? DeclaredSchemaVersion => declared_schema_version.Value;

        /// <summary>Realm 里实际的类名（<c>[MapTo]</c> 会改名，如 <c>ScoreInfo → Score</c>）。</summary>
        public static string realmClassName(Type type)
            => type.GetCustomAttribute<MapToAttribute>()?.Mapping ?? type.Name;

        public static RealmConfiguration CreateConfiguration(string realmPath, ulong schemaVersion, bool readOnly, bool allowMigration)
        {
            var config = new RealmConfiguration(Path.GetFullPath(realmPath))
            {
                Schema = ObjectTypes,
                SchemaVersion = schemaVersion,
                IsReadOnly = readOnly,

                // 与官方 RealmAccess 一致：临时目录下的 pipe 用于规避 Windows 上的文件锁问题。
                FallbackPipePath = pipeDirectory(),
            };

            // 官方客户端自带迁移回调（只做数据修补，schema 变更由 realm-core 自己施加）。
            // 这里给一个空回调就够了：我们要的是"官方包能不能接受这份文件的 schema"。
            if (allowMigration)
                config.MigrationCallback = static (_, _) => { };

            return config;
        }

        public static RealmInstance Open(RealmConfiguration config) => RealmInstance.GetInstance(config);

        /// <summary>
        /// 从 native handle 读回磁盘 schema 版本。realm-dotnet 不公开这个值，但它是"官方到底把文件
        /// 升到哪一版"的唯一直接证据，与产品侧 <c>DynamicRealmSession</c> 用同一套反射。
        /// </summary>
        public static int? TryReadSchemaVersion(RealmInstance realm)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            object? handle = typeof(RealmInstance).GetField("SharedRealmHandle", flags)?.GetValue(realm);
            MethodInfo? method = handle?.GetType().GetMethod("GetSchemaVersion", flags, binder: null, Type.EmptyTypes, modifiers: null);

            return method?.Invoke(handle, null) is ulong version && version <= int.MaxValue ? (int)version : null;
        }

        public static string pipeDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "lazer");
            Directory.CreateDirectory(path);
            return path;
        }

        private static Type[] discoverObjectTypes()
        {
            Assembly game = typeof(BeatmapSetInfo).Assembly;

            Type?[] candidates;

            try
            {
                candidates = game.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // 官方包里有可选依赖（音频/图形）时部分类型可能加载失败；能拿到多少算多少。
                candidates = ex.Types;
            }

            return candidates
                   .Where(t => t is { IsClass: true, IsAbstract: false })
                   .Where(t => t != null && (typeof(RealmObject).IsAssignableFrom(t) || typeof(EmbeddedObject).IsAssignableFrom(t)))
                   .Select(t => t!)
                   .OrderBy(t => t.FullName, StringComparer.Ordinal)
                   .ToArray();
        }

        private static int? tryReadDeclaredSchemaVersion()
        {
            Type access = typeof(Database.RealmAccess);
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (string fieldName in new[] { "schema_version", "SCHEMA_VERSION" })
            {
                FieldInfo? field = access.GetField(fieldName, flags);

                if (field is { IsLiteral: true } literal && literal.GetRawConstantValue() is int literalValue)
                    return literalValue;
            }

            foreach (string propertyName in new[] { "UpstreamSchemaVersion", "SchemaVersion" })
            {
                if (access.GetProperty(propertyName, flags)?.GetValue(null) is int propertyValue)
                    return propertyValue;
            }

            return null;
        }
    }
}
