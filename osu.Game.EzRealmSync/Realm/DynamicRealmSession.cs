using System.Reflection;
using RealmConfiguration = Realms.RealmConfiguration;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 用磁盘文件头 schema 钉死打开 DynamicRealm。禁止加载 osu.Game 模型，避免错版本污染用户库。
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

        public int DiskSchemaVersion { get; }

        public bool IsReadOnly { get; }

        public RealmInstance Realm => realm;

        public RealmInstance.Dynamic Dynamic => realm.DynamicApi;

        public static DynamicRealmSession OpenPinned(string realmFilePath, int diskSchemaVersion, bool readOnly)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(realmFilePath);

            if (diskSchemaVersion <= 0)
                throw new InvalidOperationException($"无效的磁盘 schema：{diskSchemaVersion}（{realmFilePath}）");

            string fullPath = Path.GetFullPath(realmFilePath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"找不到 Realm 文件：{fullPath}", fullPath);

            string pipeDir = EzRealmSyncDataPaths.RealmPipeDirectory;
            Directory.CreateDirectory(pipeDir);

            var config = new RealmConfiguration(fullPath)
            {
                IsDynamic = true,
                IsReadOnly = readOnly,
                SchemaVersion = (ulong)diskSchemaVersion,
                Schema = Array.Empty<Type>(),
                FallbackPipePath = pipeDir,
            };

            RealmInstance instance = RealmOpenContext.GetInstance(config);

            try
            {
                ulong handleVersion = ReadSchemaVersionFromHandle(instance);
                if (isUsableSchemaVersion(handleVersion) && handleVersion != (ulong)diskSchemaVersion)
                {
                    instance.Dispose();
                    throw new InvalidOperationException(
                        $"拒绝打开：请求钉死 schema {diskSchemaVersion}，但磁盘文件头是 {handleVersion}。{fullPath}");
                }

                return new DynamicRealmSession(instance, fullPath, diskSchemaVersion, readOnly);
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
