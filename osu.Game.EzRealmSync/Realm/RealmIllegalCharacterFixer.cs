#if HAS_EZ_OSU_GAME
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;
using osu.Game.Scoring;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    internal static class RealmIllegalCharacterFixer
    {
        public static void Scan(RealmAccess access, List<RealmFixIssue> issues, RealmFixScanOptions options)
        {
            char replacement = string.IsNullOrEmpty(options.IllegalCharacterReplacement)
                ? '_'
                : options.IllegalCharacterReplacement[0];

            access.Run(realm =>
            {
                foreach (var beatmap in realm.All<BeatmapInfo>().Where(b => b.BeatmapSet == null || !b.BeatmapSet.DeletePending))
                    scanMetadata(beatmap.Metadata, beatmap.ID, EntityKind.Beatmap, issues, options.IllegalCharacters, replacement);

                foreach (var score in realm.All<ScoreInfo>().Where(s => !s.DeletePending))
                    scanScore(score, issues, options.IllegalCharacters, replacement);
            });
        }

        public static int Apply(RealmAccess access, IReadOnlyList<RealmFixIssue> issues, CancellationToken cancellationToken)
        {
            int applied = 0;

            access.Write(realm =>
            {
                foreach (var issue in issues)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (issue.Kind != RealmFixIssueKind.IllegalCharacter || issue.TargetEntityId == null)
                        continue;

                    if (issue.EntityKind == EntityKind.Beatmap)
                    {
                        var beatmap = realm.Find<BeatmapInfo>(issue.TargetEntityId.Value);
                        if (beatmap == null)
                            continue;

                        if (applyToMetadata(realm, beatmap.Metadata, issue.FieldName, issue.SuggestedValue))
                            applied++;
                    }
                    else if (issue.EntityKind == EntityKind.BeatmapSet)
                    {
                        var set = realm.Find<BeatmapSetInfo>(issue.TargetEntityId.Value);
                        if (set == null)
                            continue;

                        foreach (var beatmap in set.Beatmaps)
                        {
                            if (applyToMetadata(realm, beatmap.Metadata, issue.FieldName, issue.SuggestedValue))
                                applied++;
                        }
                    }
                    else if (issue.EntityKind == EntityKind.Score)
                    {
                        var score = realm.Find<ScoreInfo>(issue.TargetEntityId.Value);
                        if (score == null)
                            continue;

                        if (applyToScore(realm, score, issue.FieldName, issue.SuggestedValue))
                            applied++;
                    }
                }
            });

            return applied;
        }

        private static void scanMetadata(
            BeatmapMetadata metadata,
            Guid entityId,
            EntityKind kind,
            List<RealmFixIssue> issues,
            IReadOnlyList<char> illegalCharacters,
            char replacement)
        {
            foreach (var (fieldName, value) in metadataStringFields(metadata))
            {
                foreach (char illegal in illegalCharacters)
                {
                    if (!value.Contains(illegal))
                        continue;

                    issues.Add(new RealmFixIssue
                    {
                        Id = Guid.NewGuid(),
                        Kind = RealmFixIssueKind.IllegalCharacter,
                        EntityKind = kind,
                        TargetEntityId = entityId,
                        FieldName = fieldName,
                        CurrentValue = value,
                        SuggestedValue = value.Replace(illegal, replacement),
                        Detail = $"字段 {fieldName} 包含非法字符 '{illegal}'",
                    });
                    break;
                }
            }
        }

        private static void scanScore(
            ScoreInfo score,
            List<RealmFixIssue> issues,
            IReadOnlyList<char> illegalCharacters,
            char replacement)
        {
            // 成绩一般无路径非法字符问题；保留扩展点
            if (string.IsNullOrEmpty(score.BeatmapHash))
                return;

            foreach (char illegal in illegalCharacters)
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

        private static bool applyToMetadata(RealmInstance realm, BeatmapMetadata metadata, string fieldName, string suggestedValue)
        {
            bool changed = false;

            switch (fieldName)
            {
                case nameof(BeatmapMetadata.Title):
                    if (!string.Equals(metadata.Title, suggestedValue, StringComparison.Ordinal))
                    {
                        metadata.Title = suggestedValue;
                        changed = true;
                    }

                    break;

                case nameof(BeatmapMetadata.TitleUnicode):
                    if (!string.Equals(metadata.TitleUnicode, suggestedValue, StringComparison.Ordinal))
                    {
                        metadata.TitleUnicode = suggestedValue;
                        changed = true;
                    }

                    break;

                case nameof(BeatmapMetadata.Artist):
                    if (!string.Equals(metadata.Artist, suggestedValue, StringComparison.Ordinal))
                    {
                        metadata.Artist = suggestedValue;
                        changed = true;
                    }

                    break;

                case nameof(BeatmapMetadata.ArtistUnicode):
                    if (!string.Equals(metadata.ArtistUnicode, suggestedValue, StringComparison.Ordinal))
                    {
                        metadata.ArtistUnicode = suggestedValue;
                        changed = true;
                    }

                    break;

                case nameof(BeatmapMetadata.Source):
                    if (!string.Equals(metadata.Source, suggestedValue, StringComparison.Ordinal))
                    {
                        metadata.Source = suggestedValue;
                        changed = true;
                    }

                    break;

                case nameof(BeatmapMetadata.Tags):
                    if (!string.Equals(metadata.Tags, suggestedValue, StringComparison.Ordinal))
                    {
                        metadata.Tags = suggestedValue;
                        changed = true;
                    }

                    break;
            }

            if (changed)
                realm.Add(metadata, update: true);

            return changed;
        }

        private static bool applyToScore(RealmInstance realm, ScoreInfo score, string fieldName, string suggestedValue)
        {
            if (fieldName != nameof(ScoreInfo.BeatmapHash))
                return false;

            if (string.Equals(score.BeatmapHash, suggestedValue, StringComparison.Ordinal))
                return false;

            score.BeatmapHash = suggestedValue;
            realm.Add(score, update: true);
            return true;
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
