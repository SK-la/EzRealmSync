using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.OfficialSchema.V51;

namespace osu.Game.EzRealmSync.OfficialSchema
{
    /// <summary>从官方镜像按 GUID 导出同步 Apply 包（与 ReadSidecar apply-export 同形）。</summary>
    public static class OfficialMirrorApplyExporter
    {
        public static RealmApplyExportResult Export(RealmApplyExportJob job)
        {
            try
            {
                using var realm = OfficialMirrorRealm.OpenPinned(job.SourceRealmFilePath, job.PinnedDiskSchemaVersion, readOnly: true);
                var idSet = job.ItemIds.ToHashSet();
                var bundle = new RealmSyncApplyBundle();

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
                        bundle.Collections.Add(mapCollection(collection));
                }

                return new RealmApplyExportResult
                {
                    Success = true,
                    Bundle = bundle,
                };
            }
            catch (Exception ex)
            {
                return new RealmApplyExportResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                };
            }
        }

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

        private static OfficialCollectionDto mapCollection(BeatmapCollection collection)
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
