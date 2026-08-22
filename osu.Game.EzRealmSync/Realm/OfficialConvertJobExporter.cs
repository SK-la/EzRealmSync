#if HAS_EZ_OSU_GAME
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.Models;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Skinning;

namespace osu.Game.EzRealmSync.Realm
{
    internal static class OfficialConvertJobExporter
    {
        public static OfficialConvertJob Export(RealmAccess sourceAccess, int targetUpstreamSchema, string targetRealmPath)
        {
            var job = new OfficialConvertJob
            {
                TargetUpstreamSchema = targetUpstreamSchema,
                TargetRealmPath = targetRealmPath,
            };

            var exportedBeatmapHashes = new HashSet<string>(StringComparer.Ordinal);
            var exportedBeatmapMd5Hashes = new HashSet<string>(StringComparer.Ordinal);

            sourceAccess.Run(realm =>
            {
                foreach (var ruleset in realm.All<RulesetInfo>())
                {
                    if (!OfficialConvertRulesetFilter.ShouldExportRuleset(ruleset))
                    {
                        job.FilterStats.SkippedRulesets++;
                        continue;
                    }

                    job.Rulesets.Add(mapRuleset(ruleset));
                }

                foreach (var set in realm.LiveBeatmapSets())
                {
                    if (!OfficialConvertRulesetFilter.ShouldExportBeatmapSet(set))
                    {
                        job.FilterStats.SkippedBeatmapSets++;
                        continue;
                    }

                    job.BeatmapSets.Add(mapBeatmapSet(set));

                    foreach (var beatmap in set.Beatmaps)
                    {
                        if (beatmap.Hidden)
                            continue;

                        if (!string.IsNullOrEmpty(beatmap.Hash))
                            exportedBeatmapHashes.Add(beatmap.Hash);

                        if (!string.IsNullOrEmpty(beatmap.MD5Hash))
                            exportedBeatmapMd5Hashes.Add(beatmap.MD5Hash);
                    }
                }

                foreach (var score in realm.LiveScores())
                {
                    if (!OfficialConvertScoreFilter.ShouldExportScore(score, exportedBeatmapHashes))
                    {
                        job.FilterStats.SkippedScores++;
                        continue;
                    }

                    job.Scores.Add(mapScore(score));
                }

                foreach (var collection in realm.All<BeatmapCollection>())
                    job.Collections.Add(mapCollection(collection, exportedBeatmapMd5Hashes, job.FilterStats));

                foreach (var file in realm.All<RealmFile>())
                    job.FileHashes.Add(file.Hash);

                foreach (var skin in realm.All<SkinInfo>())
                {
                    if (OfficialConvertSkinFilter.ShouldExcludeFromOfficial(skin))
                    {
                        job.FilterStats.SkippedSkins++;
                        continue;
                    }

                    job.Skins.Add(mapSkin(skin));
                }
            });

            return job;
        }

        /// <summary>按 GUID 导出同步 Apply 包（不过滤 Ez 规则集；收藏夹保留全部 MD5）。</summary>
        public static RealmSyncApplyBundle ExportPartialByIds(RealmAccess sourceAccess, IReadOnlyList<Guid> itemIds)
        {
            var idSet = itemIds.ToHashSet();
            var bundle = new RealmSyncApplyBundle();

            sourceAccess.Run(realm =>
            {
                foreach (Guid id in idSet)
                {
                    if (realm.Find<BeatmapSetInfo>(id) is BeatmapSetInfo set && !set.DeletePending)
                    {
                        bundle.BeatmapSets.Add(mapBeatmapSet(set));
                        continue;
                    }

                    if (realm.Find<BeatmapInfo>(id) is BeatmapInfo beatmap && beatmap.BeatmapSet?.DeletePending != true)
                    {
                        bundle.Beatmaps.Add(mapBeatmap(beatmap));
                        continue;
                    }

                    if (realm.Find<ScoreInfo>(id) is ScoreInfo score && !score.DeletePending)
                    {
                        bundle.Scores.Add(mapScore(score));
                        continue;
                    }

                    if (realm.Find<BeatmapCollection>(id) is BeatmapCollection collection)
                        bundle.Collections.Add(mapCollectionForSync(collection));
                }
            });

            return bundle;
        }

        private static OfficialCollectionDto mapCollectionForSync(BeatmapCollection collection)
        {
            var dto = new OfficialCollectionDto
            {
                ID = collection.ID,
                Name = collection.Name,
                LastModified = collection.LastModified,
            };

            foreach (string md5 in collection.BeatmapMD5Hashes)
                dto.BeatmapMD5Hashes.Add(md5);

            return dto;
        }

        private static OfficialRulesetDto mapRuleset(RulesetInfo ruleset) =>
            new OfficialRulesetDto
            {
                ShortName = ruleset.ShortName,
                OnlineID = ruleset.OnlineID,
                Name = ruleset.Name,
                InstantiationInfo = ruleset.InstantiationInfo,
                LastAppliedDifficultyVersion = ruleset.LastAppliedDifficultyVersion,
                Available = ruleset.Available,
            };

        private static OfficialBeatmapSetDto mapBeatmapSet(BeatmapSetInfo set)
        {
            var dto = new OfficialBeatmapSetDto
            {
                ID = set.ID,
                OnlineID = set.OnlineID,
                DateAdded = set.DateAdded,
                DateSubmitted = set.DateSubmitted,
                DateRanked = set.DateRanked,
                StatusInt = set.StatusInt,
                DeletePending = set.DeletePending,
                Hash = set.Hash,
                Protected = set.Protected,
            };

            foreach (var file in set.Files)
                dto.Files.Add(mapFileUsage(file));

            foreach (var beatmap in set.Beatmaps)
            {
                if (beatmap.Hidden)
                    continue;

                dto.Beatmaps.Add(mapBeatmap(beatmap));
            }

            return dto;
        }

        private static OfficialBeatmapDto mapBeatmap(BeatmapInfo beatmap) =>
            new OfficialBeatmapDto
            {
                ID = beatmap.ID,
                DifficultyName = beatmap.DifficultyName,
                RulesetShortName = beatmap.Ruleset.ShortName,
                Difficulty = new OfficialBeatmapDifficultyDto
                {
                    DrainRate = beatmap.Difficulty.DrainRate,
                    CircleSize = beatmap.Difficulty.CircleSize,
                    OverallDifficulty = beatmap.Difficulty.OverallDifficulty,
                    ApproachRate = beatmap.Difficulty.ApproachRate,
                    SliderMultiplier = beatmap.Difficulty.SliderMultiplier,
                    SliderTickRate = beatmap.Difficulty.SliderTickRate,
                },
                Metadata = mapMetadata(beatmap.Metadata),
                UserSettings = new OfficialBeatmapUserSettingsDto { Offset = beatmap.UserSettings.Offset },
                StatusInt = beatmap.StatusInt,
                OnlineID = beatmap.OnlineID,
                Length = beatmap.Length,
                BPM = beatmap.BPM,
                Hash = beatmap.Hash,
                StarRating = beatmap.StarRating,
                MD5Hash = beatmap.MD5Hash,
                OnlineMD5Hash = beatmap.OnlineMD5Hash,
                LastLocalUpdate = beatmap.LastLocalUpdate,
                LastOnlineUpdate = beatmap.LastOnlineUpdate,
                Hidden = beatmap.Hidden,
                EndTimeObjectCount = beatmap.EndTimeObjectCount,
                TotalObjectCount = beatmap.TotalObjectCount,
                LastPlayed = beatmap.LastPlayed,
                BeatDivisor = beatmap.BeatDivisor,
                EditorTimestamp = beatmap.EditorTimestamp,
            };

        private static OfficialBeatmapMetadataDto mapMetadata(BeatmapMetadata metadata)
        {
            var dto = new OfficialBeatmapMetadataDto
            {
                Title = metadata.Title,
                TitleUnicode = metadata.TitleUnicode,
                Artist = metadata.Artist,
                ArtistUnicode = metadata.ArtistUnicode,
                Author = mapUser(metadata.Author),
                Source = metadata.Source,
                Tags = metadata.Tags,
                PreviewTime = metadata.PreviewTime,
                AudioFile = metadata.AudioFile,
                BackgroundFile = metadata.BackgroundFile,
            };

            foreach (string tag in metadata.UserTags)
                dto.UserTags.Add(tag);

            return dto;
        }

        private static OfficialScoreDto mapScore(ScoreInfo score)
        {
            var dto = new OfficialScoreDto
            {
                ID = score.ID,
                BeatmapHash = score.BeatmapHash,
                RulesetShortName = score.Ruleset.ShortName,
                ClientVersion = score.ClientVersion,
                Hash = score.Hash,
                DeletePending = score.DeletePending,
                TotalScore = score.TotalScore,
                TotalScoreWithoutMods = score.TotalScoreWithoutMods,
                TotalScoreVersion = score.TotalScoreVersion,
                LegacyTotalScore = score.LegacyTotalScore,
                BackgroundReprocessingFailed = score.BackgroundReprocessingFailed,
                MaxCombo = score.MaxCombo,
                Accuracy = score.Accuracy,
                Date = score.Date,
                PP = score.PP,
                OnlineID = score.OnlineID,
                LegacyOnlineID = score.LegacyOnlineID,
                User = mapUser(score.RealmUser),
                ModsJson = score.ModsJson,
                StatisticsJson = score.StatisticsJson,
                MaximumStatisticsJson = score.MaximumStatisticsJson,
                RankInt = score.RankInt,
                Combo = score.Combo,
                IsLegacyScore = score.IsLegacyScore,
            };

            foreach (int pause in score.Pauses)
                dto.Pauses.Add(pause);

            foreach (var file in score.Files)
                dto.Files.Add(mapFileUsage(file));

            return dto;
        }

        private static OfficialCollectionDto mapCollection(
            BeatmapCollection collection,
            IReadOnlySet<string> exportedBeatmapMd5Hashes,
            OfficialConvertFilterStats stats)
        {
            var dto = new OfficialCollectionDto
            {
                ID = collection.ID,
                Name = collection.Name,
                LastModified = collection.LastModified,
            };

            foreach (string md5 in collection.BeatmapMD5Hashes)
            {
                if (exportedBeatmapMd5Hashes.Contains(md5))
                {
                    dto.BeatmapMD5Hashes.Add(md5);
                    continue;
                }

                stats.PrunedCollectionEntries++;
            }

            return dto;
        }

        private static OfficialSkinDto mapSkin(SkinInfo skin)
        {
            var dto = new OfficialSkinDto
            {
                ID = skin.ID,
                Name = skin.Name,
                Creator = skin.Creator,
                InstantiationInfo = skin.InstantiationInfo,
                Hash = skin.Hash,
                Protected = skin.Protected,
                DeletePending = skin.DeletePending,
            };

            foreach (var file in skin.Files)
                dto.Files.Add(mapFileUsage(file));

            return dto;
        }

        private static OfficialNamedFileDto mapFileUsage(RealmNamedFileUsage usage) =>
            new OfficialNamedFileDto
            {
                Hash = usage.File.Hash,
                Filename = usage.Filename,
            };

        private static OfficialRealmUserDto mapUser(RealmUser user) =>
            new OfficialRealmUserDto
            {
                OnlineID = user.OnlineID,
                Username = user.Username,
                CountryString = user.CountryString,
            };
    }
}
#endif
