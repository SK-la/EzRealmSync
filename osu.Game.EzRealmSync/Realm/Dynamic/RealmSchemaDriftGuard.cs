namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 「只填单元格」的硬校验：动态写入不提供 schema，Realm 不可能加类或加列，
    /// 因此写前写后的全量 schema 快照必须逐类逐列一字不差。
    /// 一旦不一致即视为本工具的 bug，宁可整笔回滚也不提交。
    /// </summary>
    public static class RealmSchemaDriftGuard
    {
        public static IReadOnlyList<string> Check(RealmSchemaSnapshot before, RealmSchemaSnapshot after)
        {
            ArgumentNullException.ThrowIfNull(before);
            ArgumentNullException.ThrowIfNull(after);

            return after.FindDifferences(before);
        }

        public static void EnsureUnchanged(RealmSchemaSnapshot before, RealmSchemaSnapshot after, string realmFilePath)
        {
            IReadOnlyList<string> drift = Check(before, after);
            if (drift.Count == 0)
                return;

            throw new InvalidOperationException(
                $"写入改变了目标库 schema（本工具只填目标已有的表与列，出现此情况请报告）：{realmFilePath}\n  "
                + string.Join("\n  ", drift));
        }
    }
}
