using System.Reflection;
using Realms.Schema;
using RealmConfiguration = Realms.RealmConfiguration;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 动态打开任意 Realm 文件（不传 schema，禁止加载 osu.Game 模型）。
    ///
    /// 打开时<b>不</b>指定 schema 版本：动态打开的版本号是惰性的（realm-core 只在
    /// <c>schema</c> 非空时才调用 <c>update_schema</c>），因此不存在按版本号拒绝或迁移的路径；
    /// 版本只作为识别/展示信息从已打开的库上读回。
    /// </summary>
    public sealed class DynamicRealmSession : IDisposable
    {
        private readonly RealmInstance realm;
        private bool disposed;

        private DynamicRealmSession(RealmInstance realm, string filePath, int diskSchemaVersion, bool readOnly)
        {
            this.realm = realm;
            FilePath = filePath;
            DiskSchemaVersion = diskSchemaVersion;
            IsReadOnly = readOnly;
        }

        public string FilePath { get; }

        /// <summary>打开后从 native handle 读回的磁盘 schema 版本；仅识别/展示用，不参与拒绝或选型。</summary>
        public int DiskSchemaVersion { get; }

        public bool IsReadOnly { get; }

        public RealmInstance Realm => realm;

        public RealmInstance.Dynamic Dynamic => realm.DynamicApi;

        public static DynamicRealmSession OpenDynamic(string realmFilePath, bool readOnly)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(realmFilePath);

            string fullPath = Path.GetFullPath(realmFilePath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"找不到 Realm 文件：{fullPath}", fullPath);

            string pipeDir = EzRealmSyncDataPaths.RealmPipeDirectory;
            Directory.CreateDirectory(pipeDir);

            var config = new RealmConfiguration(fullPath)
            {
                IsDynamic = true,
                IsReadOnly = readOnly,
                Schema = Array.Empty<Type>(),
                FallbackPipePath = pipeDir,
            };

            RealmInstance instance = RealmOpenContext.GetInstance(config);

            try
            {
                ulong handleVersion = ReadSchemaVersionFromHandle(instance);

                return new DynamicRealmSession(instance, fullPath, isUsableSchemaVersion(handleVersion) ? (int)handleVersion : 0, readOnly);
            }
            catch
            {
                instance.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 带显式 schema 打开动态库：文件不存在时按该 schema 新建，已存在时以该 schema 为基准做一次
        /// schema 更新。与 <see cref="OpenDynamic"/> 相反，这里<b>会</b>触发迁移——"把数据搬进官方 schema
        /// 的新库"正是需要它。收窄既有文件（丢掉 Ez 表/列）不在支持范围内：Realm 不允许把 schema
        /// 版本降回官方号，所以这类请求一律落到新建文件上。
        /// </summary>
        public static DynamicRealmSession OpenDynamicWithSchema(
            string realmFilePath,
            RealmSchema schema,
            ulong schemaVersion,
            bool readOnly)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(realmFilePath);
            ArgumentNullException.ThrowIfNull(schema);

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(realmFilePath))!);

            string pipeDir = EzRealmSyncDataPaths.RealmPipeDirectory;
            Directory.CreateDirectory(pipeDir);

            var config = new RealmConfiguration(Path.GetFullPath(realmFilePath))
            {
                IsDynamic = true,
                IsReadOnly = readOnly,
                Schema = schema,
                SchemaVersion = schemaVersion,
                FallbackPipePath = pipeDir,
            };

            RealmInstance instance = RealmOpenContext.GetInstance(config);

            try
            {
                ulong handleVersion = ReadSchemaVersionFromHandle(instance);

                return new DynamicRealmSession(
                    instance,
                    Path.GetFullPath(realmFilePath),
                    isUsableSchemaVersion(handleVersion) ? (int)handleVersion : 0,
                    readOnly);
            }
            catch
            {
                instance.Dispose();
                throw;
            }
        }

        public static ulong ReadSchemaVersionFromHandle(RealmInstance instance)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            object? handle = typeof(RealmInstance).GetField("SharedRealmHandle", flags)?.GetValue(instance);
            if (handle == null)
                return 0;

            MethodInfo? method = handle.GetType().GetMethod("GetSchemaVersion", flags, binder: null, Type.EmptyTypes, modifiers: null);
            if (method == null)
                return 0;

            ulong version = (ulong)method.Invoke(handle, null)!;
            return isUsableSchemaVersion(version) ? version : 0;
        }

        private static bool isUsableSchemaVersion(ulong version) =>
            version > 0 && version <= int.MaxValue;

        public bool HasClass(string className) => realm.Schema.TryFindObjectSchema(className, out _);

        public bool HasProperty(string className, string propertyName)
        {
            if (!realm.Schema.TryFindObjectSchema(className, out var schema))
                return false;

            return schema.Any(p => string.Equals(p.Name, propertyName, StringComparison.Ordinal));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            realm.Dispose();
        }
    }
}
