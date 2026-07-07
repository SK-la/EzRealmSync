#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.Models;

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
                if (RealmSchemaSafety.RequiresOfficialRealmAccess(schema))
                    return RealmDiffReader.OpenOfficialRealm(realmFilePath, schema.Value);

                if (RealmSchemaSafety.RequiresEzRealmAccess(schema))
                    return RealmDiffReader.OpenEzRealm(realmFilePath, schema.Value);
            }
            catch (Exception ex) when (isMigrationRequiredError(ex))
            {
                string fileName = Path.GetFileName(realmFilePath);
                throw new RealmUserOperationException(
                    RealmUserErrorKind.MigrationRequired,
                    $"Realm 文件 {fileName} 需要 schema 迁移才能打开。EzRealmSync 不执行迁移，请先用对应客户端启动一次完成升级（官方库用 osu!lazer，Ez 库用 Ez2Lazer），然后再重试。",
                    ex);
            }

            throw new InvalidOperationException($"无法识别的 Realm schema 版本 {schema}：{realmFilePath}");
        }

        private static bool isMigrationRequiredError(Exception ex)
        {
            string message = ex.InnerException?.Message ?? ex.Message;
            return message.Contains("Migration is required", StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
