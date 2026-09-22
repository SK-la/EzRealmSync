using System.Collections;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm.Dynamic;
using Realms;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 谱面元数据 / 成绩哈希里的非法字符修复。
    ///
    /// 动态读写：只碰官方基线列（元数据 6 个文本列 + <c>Score.BeatmapHash</c>），
    /// 不改 schema、不动 Ez 列。写前先重读当前值，避免"诊断时的值"和"落库时的值"不是同一份。
    /// </summary>
    internal static class RealmIllegalCharacterFixer
    {
        /// <summary>元数据里可能含非法字符的文本列（与官方展示 / 导出文件名相关的字段）。</summary>
        private static readonly string[] metadata_text_fields = ["Title", "TitleUnicode", "Artist", "ArtistUnicode", "Source", "Tags"];

        public static void Scan(DynamicRealmSession session, RealmSchemaSnapshot schema, List<RealmFixIssue> issues, RealmFixScanOptions options)
        {
            char replacement = string.IsNullOrEmpty(options.IllegalCharacterReplacement)
                ? '_'
                : options.IllegalCharacterReplacement[0];

            foreach (IRealmObjectBase beatmap in DynamicRowAccess.LiveRows(session, schema, OfficialBaselineSchema.Beatmap))
                scanEntity(beatmap, schema, EntityKind.Beatmap, issues, options.IllegalCharacters, replacement, metadata_text_fields, "Metadata.");

            foreach (IRealmObjectBase score in DynamicRowAccess.LiveRows(session, schema, OfficialBaselineSchema.Score))
                scanEntity(score, schema, EntityKind.Score, issues, options.IllegalCharacters, replacement, ["BeatmapHash"], string.Empty);
        }

        public static int Apply(DynamicRealmSession session, RealmSchemaSnapshot schema, IReadOnlyList<RealmFixIssue> issues, CancellationToken cancellationToken)
        {
            int applied = 0;

            using (var transaction = session.Realm.BeginWrite())
            {
                foreach (var issue in issues)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (issue.Kind != RealmFixIssueKind.IllegalCharacter || issue.TargetEntityId == null)
                        continue;

                    applied += applyOne(session, schema, issue);
                }

                transaction.Commit();
            }

            return applied;
        }

        private static int applyOne(DynamicRealmSession session, RealmSchemaSnapshot schema, RealmFixIssue issue)
        {
            if (issue.TargetEntityId is not { } targetId)
                return 0;

            switch (issue.EntityKind)
            {
                case EntityKind.Beatmap:
                    return applyToBeatmaps(session, schema, [DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.Beatmap, targetId)], issue);

                case EntityKind.BeatmapSet:
                {
                    // 谱面集自己没有元数据列，修复范围是它下面所有难度。
                    if (DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.BeatmapSet, targetId) is not { } set)
                        return 0;

                    if (DynamicRowAccess.Resolve(set, schema, "Beatmaps") is not IEnumerable beatmaps)
                        return 0;

                    var targets = new List<IRealmObjectBase?>();

                    foreach (object? beatmap in beatmaps)
                        targets.Add(beatmap as IRealmObjectBase);

                    return applyToBeatmaps(session, schema, targets, issue);
                }

                case EntityKind.Score:
                {
                    if (DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.Score, targetId) is not { } score)
                        return 0;

                    if (!string.Equals(issue.FieldName, "BeatmapHash", StringComparison.Ordinal))
                        return 0;

                    // 以库里的当前值为准：诊断之后用户可能已经手改过，别用旧诊断值覆盖新值。
                    if (DynamicRowAccess.ResolveString(score, schema, "BeatmapHash") is string current
                        && string.Equals(current, issue.SuggestedValue, StringComparison.Ordinal))
                    {
                        return 0;
                    }

                    DynamicRealmAccess.Set(score, OfficialBaselineSchema.Score, "BeatmapHash", issue.SuggestedValue);
                    return 1;
                }

                default:
                    return 0;
            }
        }

        private static int applyToBeatmaps(DynamicRealmSession session, RealmSchemaSnapshot schema, IEnumerable<IRealmObjectBase?> beatmaps, RealmFixIssue issue)
        {
            if (Array.IndexOf(metadata_text_fields, issue.FieldName) < 0)
                return 0;

            int applied = 0;

            foreach (IRealmObjectBase? beatmap in beatmaps)
            {
                if (beatmap == null)
                    continue;

                // 元数据列在嵌入对象上：直接对该嵌入对象写列即可，父行已经在库里、无需额外 Add。
                if (DynamicRowAccess.Resolve(beatmap, schema, "Metadata") is not IRealmObjectBase metadata)
                    continue;

                if (DynamicRowAccess.ResolveString(metadata, schema, issue.FieldName) is not string current)
                    continue;

                if (string.Equals(current, issue.SuggestedValue, StringComparison.Ordinal))
                    continue;

                DynamicRealmAccess.Set(metadata, OfficialBaselineSchema.BeatmapMetadata, issue.FieldName, issue.SuggestedValue);
                applied++;
            }

            return applied;
        }

        private static void scanEntity(
            IRealmObjectBase row,
            RealmSchemaSnapshot schema,
            EntityKind kind,
            List<RealmFixIssue> issues,
            IReadOnlyList<char> illegalCharacters,
            char replacement,
            IReadOnlyList<string> fields,
            string prefix)
        {
            foreach (string field in fields)
            {
                if (DynamicRowAccess.Resolve(row, schema, prefix + field) is not string value || value.Length == 0)
                    continue;

                foreach (char illegal in illegalCharacters)
                {
                    if (!value.Contains(illegal))
                        continue;

                    issues.Add(new RealmFixIssue
                    {
                        Id = Guid.NewGuid(),
                        Kind = RealmFixIssueKind.IllegalCharacter,
                        EntityKind = kind,
                        TargetEntityId = readRowId(row, schema),
                        FieldName = field,
                        CurrentValue = value,
                        SuggestedValue = value.Replace(illegal, replacement),
                        Detail = prefix.Length == 0
                            ? $"Hash 包含非法字符 '{illegal}'"
                            : $"字段 {field} 包含非法字符 '{illegal}'",
                    });

                    // 一个字段只报第一个非法字符：后续修复会把整段值替换掉，逐字符各报一条是同一处缺陷重复计数。
                    break;
                }
            }
        }

        /// <summary>修复条目靠主键回查对象；诊断对象没有 Guid 主键的类不参与修复页。</summary>
        private static Guid readRowId(IRealmObjectBase row, RealmSchemaSnapshot schema) =>
            DynamicRowAccess.Resolve(row, schema, "ID") is Guid id ? id : Guid.Empty;
    }
}
