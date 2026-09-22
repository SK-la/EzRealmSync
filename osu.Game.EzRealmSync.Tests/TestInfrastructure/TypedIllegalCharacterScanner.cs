#if HAS_EZ_OSU_GAME
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;
using osu.Game.Scoring;

namespace osu.Game.EzRealmSync.Tests.TestInfrastructure
{
    /// <summary>
    /// 非法字符扫描的 **typed 参考实现**，只给对照测试用（产品侧已改为动态扫描）。
    /// 逻辑照抄改造前的产品实现，用来证明动态版报出的条目没有多报 / 少报 / 报错字段。
    /// </summary>
    internal static class TypedIllegalCharacterScanner
    {
        public static void Scan(RealmAccess access, List<RealmFixIssue> issues, RealmFixScanOptions options)
        {
            char replacement = string.IsNullOrEmpty(options.IllegalCharacterReplacement)
                ? '_'
                : options.IllegalCharacterReplacement[0];

            access.Run(realm =>
            {
                foreach (var beatmap in realm.LiveBeatmaps())
                {
                    foreach (var (fieldName, value) in metadataStringFields(beatmap.Metadata))
                    {
                        foreach (char illegal in options.IllegalCharacters)
                        {
                            if (!value.Contains(illegal))
                                continue;

                            issues.Add(new RealmFixIssue
                            {
                                Id = Guid.NewGuid(),
                                Kind = RealmFixIssueKind.IllegalCharacter,
                                EntityKind = EntityKind.Beatmap,
                                TargetEntityId = beatmap.ID,
                                FieldName = fieldName,
                                CurrentValue = value,
                                SuggestedValue = value.Replace(illegal, replacement),
                                Detail = $"字段 {fieldName} 包含非法字符 '{illegal}'",
                            });
                            break;
                        }
                    }
                }

                foreach (var score in realm.LiveScores())
                {
                    if (string.IsNullOrEmpty(score.BeatmapHash))
                        continue;

                    foreach (char illegal in options.IllegalCharacters)
                    {
                        if (!score.BeatmapHash.Contains(illegal))
                            continue;

                        issues.Add(new RealmFixIssue
                        {
                            Id = Guid.NewGuid(),
                            Kind = RealmFixIssueKind.IllegalCharacter,
                            EntityKind = EntityKind.Score,
                            TargetEntityId = score.ID,
                            FieldName = nameof(ScoreInfo.BeatmapHash),
                            CurrentValue = score.BeatmapHash,
                            SuggestedValue = score.BeatmapHash.Replace(illegal, replacement),
                            Detail = $"Hash 包含非法字符 '{illegal}'",
                        });
                        break;
                    }
                }
            });
        }

        private static IEnumerable<(string FieldName, string Value)> metadataStringFields(BeatmapMetadata metadata)
        {
            yield return (nameof(BeatmapMetadata.Title), metadata.Title);
            yield return (nameof(BeatmapMetadata.TitleUnicode), metadata.TitleUnicode);
            yield return (nameof(BeatmapMetadata.Artist), metadata.Artist);
            yield return (nameof(BeatmapMetadata.ArtistUnicode), metadata.ArtistUnicode);
            yield return (nameof(BeatmapMetadata.Source), metadata.Source);
            yield return (nameof(BeatmapMetadata.Tags), metadata.Tags);
        }
    }
}
#endif
