#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm.Readers;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 根据磁盘 schema 选择 <see cref="RealmAccess"/> 打开方式。探测与打开均不迁移 schema。
    /// </summary>
    public static class RealmSchemaProbe
    {
        /// <summary>仅读取文件头 schema，不迁移、不写盘。</summary>
        public static int? TryReadSchemaVersion(string realmFilePath) => RealmDiskSchemaReader.TryReadSchemaVersion(realmFilePath);

        /// <summary>按磁盘版本打开，<b>绝不</b>执行 Realm 迁移。</summary>
        public static RealmAccess Open(string realmFilePath, int? diskSchemaVersion = null)
        {
            int? schema = diskSchemaVersion ?? TryReadSchemaVersion(realmFilePath);

            if (schema == null)
            {
                RealmDiskSchemaReader.TryReadSchemaVersion(realmFilePath, out string? detail);
                string suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" {detail}";
                throw new InvalidOperationException($"无法读取 Realm schema 版本：{realmFilePath}.{suffix}");
            }

            RealmSchemaToolPolicy.EnsureCanOpen(schema.Value);

            try
            {
                return RealmReaderRegistry.Instance.Router.OpenByDiskSchemaVersion(schema.Value, realmFilePath);
            }
            catch (RealmUserOperationException)
            {
                throw;
            }
            catch (Exception ex) when (RealmOpenErrorClassifier.IsMigrationRequired(ex))
            {
                string fileName = Path.GetFileName(realmFilePath);
                bool atLatest = RealmSchemaToolPolicy.IsAtLatestSupported(schema.Value);

                if (atLatest)
                {
                    throw new RealmUserOperationException(
                        RealmUserErrorKind.SchemaModelMismatch,
                        $"Realm 文件 {fileName} 磁盘 schema 已是本工具最新（{schema}），但对象模型不匹配，无法打开。请更新 EzRealmSync 使其与写出该库的客户端一致，或从完整备份恢复。",
                        ex);
                }

                throw new RealmUserOperationException(
                    RealmUserErrorKind.MigrationRequired,
                    $"Realm 文件 {fileName}（schema {schema}）需要先升到本工具当前版本（{RealmSchemaToolPolicy.LatestSupportedForKind(RealmSchemaSafety.Classify(schema.Value))}）才能打开。请在「修复」页点击「升级到最新版」，或使用「转回官方版」（会自动先升级）。",
                    ex);
            }
        }
    }
}
#endif
