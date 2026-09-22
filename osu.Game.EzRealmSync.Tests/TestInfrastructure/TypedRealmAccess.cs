using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;

namespace osu.Game.EzRealmSync.Tests.TestInfrastructure
{
    /// <summary>
    /// parity 测试的 typed 参照面：产品侧已全部改走 DynamicRealm，这里保留一份最小 typed 打开入口，
    /// 只供测试写/读同一份库做逐列对照，<b>不得</b>被产品工程引用。
    /// 按磁盘 schema 选 Ez / 官方 typed 模型，pinned 版本原样透传（老样本按各自版本打开，不迁移）。
    /// </summary>
    public static class TypedRealmAccess
    {
        public static RealmAccess OpenForMutation(string realmFilePath, int? diskSchemaVersion = null)
        {
            int schema = diskSchemaVersion
                         ?? RealmDiskSchemaReader.TryReadSchemaVersion(realmFilePath)
                         ?? throw new InvalidOperationException($"无法读取 Realm schema 版本：{realmFilePath}");

            string fullPath = Path.GetFullPath(realmFilePath);
            string storageRoot = RealmWorkspacePaths.ResolveStorageRoot(fullPath);
            string filename = RealmWorkspacePaths.ResolveStorageRelativeRealmPath(fullPath);
            var storage = new NativeStorage(storageRoot);

            return RealmOpenContext.WithoutCapturedContext(() => RealmSchemaSafety.IsOfficialDiskSchema(schema)
                ? OfficialRealmAccess.OpenWithoutMigration(storage, filename, schema)
                : RealmAccess.OpenWithoutMigration(storage, filename, schema));
        }
    }
}
