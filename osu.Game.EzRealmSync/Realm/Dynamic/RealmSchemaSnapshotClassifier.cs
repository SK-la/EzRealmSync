using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm.Dynamic
{
    /// <summary>
    /// 用快照内容判断一份库能否当「官方 N 的事实来源」。
    ///
    /// 判据两道：磁盘版本解码出的 Ez 修订 <c>E == 0</c>（纯官方号，如 52），且快照无 Ez 特征
    /// （<c>Ez*</c> 类、已知 Ez 列、或官方表上多出来的列）。全部依据内容与版本，不需要 DLL，
    /// 也不需要预置版本名单。
    ///
    /// 判据刻意偏向"不是官方"：把 Ez 库误当官方基线会污染写集，代价远大于把官方库误判为非官方
    /// （后者只退化为保守白名单）。官方版本比本工具已知的更新时，正是靠这条偏向保安全。
    /// </summary>
    public static class RealmSchemaSnapshotClassifier
    {
        public static bool TryGetOfficialUpstream(int diskSchemaVersion, RealmSchemaSnapshot snapshot, out int officialUpstream)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            officialUpstream = 0;

            var (official, ez) = RealmSchemaVersions.Decode(diskSchemaVersion);
            if (official <= 0 || ez != 0)
                return false;

            if (HasEzFingerprint(snapshot))
                return false;

            officialUpstream = official;
            return true;
        }

        public static bool HasEzFingerprint(RealmSchemaSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            foreach (RealmClassSchema schema in snapshot.Classes)
            {
                // Ez 新增的表都叫 Ez*（EzDanEstimate、EzBeatmapSkillValue…），官方表不会以 Ez 开头。
                if (schema.Name.StartsWith("Ez", StringComparison.Ordinal))
                    return true;

                foreach (RealmPropertySchema property in schema.Properties)
                {
                    if (IsEzOnlyProperty(schema.Name, property.Name))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Ez 在官方表上新增的列：显式名单，或「官方基线里没有这一列」。
        /// 后者覆盖了 <c>Score.Passed</c> 这类没进 <see cref="OfficialBaselineSchema.EzOnlyPropertyNames"/> 的加列。
        /// </summary>
        public static bool IsEzOnlyProperty(string className, string propertyName)
        {
            if (OfficialBaselineSchema.EzOnlyPropertyNames.Contains(propertyName, StringComparer.Ordinal))
                return true;

            return OfficialBaselineSchema.KnownProperties.ContainsKey(className)
                   && !OfficialBaselineSchema.IsKnownProperty(className, propertyName);
        }
    }
}
