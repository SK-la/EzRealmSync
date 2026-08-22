using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.OfficialSchema.V51;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.OfficialSchema
{
    public static class OfficialMirrorRealmWriter
    {
        public static OfficialConvertResult Write(OfficialConvertJob job)
        {
            string targetPath = Path.GetFullPath(job.TargetRealmPath);
            int schema = job.TargetUpstreamSchema;

            try
            {
                OfficialMirrorRealm.CreateEmpty(targetPath, schema);

                var realm = OfficialMirrorRealm.OpenPinned(targetPath, schema);

                int applied;

                try
                {
                    applied = realm.Write(writeRulesets(job.Rulesets));
                    applied += writeBeatmapSets(realm, job.BeatmapSets);
                    applied += writeScores(realm, job.Scores);
                    applied += writeCollections(realm, job.Collections);
                    applied += realm.Write(writeSkins(job.Skins, job.FileHashes));
                }
                finally
                {
                    realm.Dispose();
                }

                using var verify = OfficialMirrorRealm.OpenPinned(targetPath, schema, readOnly: true);

                int fileCount = verify.All<RealmFile>().Count();

                return new OfficialConvertResult
                {
                    Success = true,
                    AppliedCount = applied,
                    RealmFileCount = fileCount,
                    TargetSchemaVersion = schema,
                    FilterStats = job.FilterStats,
                };
            }
            catch (Exception ex)
            {
                return new OfficialConvertResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    TargetSchemaVersion = schema,
                    FilterStats = job.FilterStats,
                };
            }
        }

        private static Func<RealmInstance, int> writeRulesets(IReadOnlyList<OfficialRulesetDto> rulesets) =>
            r =>
            {
                int added = 0;

                foreach (var dto in rulesets)
                {
                    if (r.All<RulesetInfo>().Any(x => x.ShortName == dto.ShortName))
                        continue;

                    r.Add(new RulesetInfo
                    {
                        ShortName = dto.ShortName,
                        OnlineID = dto.OnlineID,
                        Name = dto.Name,
                        InstantiationInfo = dto.InstantiationInfo,
                        LastAppliedDifficultyVersion = dto.LastAppliedDifficultyVersion,
                        Available = dto.Available,
                    });
                    added++;
                }

                return added;
            };

        private static Func<RealmInstance, int> writeSkins(IReadOnlyList<OfficialSkinDto> skins, IReadOnlyList<string> fileHashes) =>
            r =>
            {
                foreach (string hash in fileHashes)
                {
                    if (r.Find<RealmFile>(hash) == null)
                        r.Add(new RealmFile { Hash = hash }, true);
                }

                int added = 0;

                foreach (var skinDto in skins)
                {
                    if (r.Find<SkinInfo>(skinDto.ID) != null)
                        continue;

                    var skin = new SkinInfo
                    {
                        ID = skinDto.ID,
                        Name = skinDto.Name,
                        Creator = skinDto.Creator,
                        InstantiationInfo = skinDto.InstantiationInfo,
                        Hash = skinDto.Hash,
                        Protected = skinDto.Protected,
                        DeletePending = skinDto.DeletePending,
                    };

                    linkFiles(r, skin.Files, skinDto.Files);
                    r.Add(skin);
                    added++;
                }

                return added;
            };

        private static int writeBeatmapSets(RealmInstance realm, IReadOnlyList<OfficialBeatmapSetDto> sets) =>
            realm.Write(r =>
            {
                int added = 0;

                foreach (var setDto in sets)
                {
                    if (r.Find<BeatmapSetInfo>(setDto.ID) != null)
                        continue;

                    var set = new BeatmapSetInfo
                    {
                        ID = setDto.ID,
                        OnlineID = setDto.OnlineID,
                        DateAdded = setDto.DateAdded,
                        DateSubmitted = setDto.DateSubmitted,
                        DateRanked = setDto.DateRanked,
                        StatusInt = setDto.StatusInt,
                        DeletePending = setDto.DeletePending,
                        Hash = setDto.Hash,
                        Protected = setDto.Protected,
                    };

                    linkFiles(r, set.Files, setDto.Files);

                    foreach (var beatmapDto in setDto.Beatmaps)
                    {
                        var beatmap = new BeatmapInfo
                        {
                            ID = beatmapDto.ID,
                            DifficultyName = beatmapDto.DifficultyName,
                            Ruleset = resolveRuleset(r, beatmapDto.RulesetShortName),
                            Difficulty = mapDifficulty(beatmapDto.Difficulty),
                            Metadata = mapMetadata(beatmapDto.Metadata),
                            UserSettings = new BeatmapUserSettings { Offset = beatmapDto.UserSettings.Offset },
                            BeatmapSet = set,
                            StatusInt = beatmapDto.StatusInt,
                            OnlineID = beatmapDto.OnlineID,
                            Length = beatmapDto.Length,
                            BPM = beatmapDto.BPM,
                            Hash = beatmapDto.Hash,
                            StarRating = beatmapDto.StarRating,
                            MD5Hash = beatmapDto.MD5Hash,
                            OnlineMD5Hash = beatmapDto.OnlineMD5Hash,
                            LastLocalUpdate = beatmapDto.LastLocalUpdate,
                            LastOnlineUpdate = beatmapDto.LastOnlineUpdate,
                            Hidden = beatmapDto.Hidden,
                            EndTimeObjectCount = beatmapDto.EndTimeObjectCount,
                            TotalObjectCount = beatmapDto.TotalObjectCount,
                            LastPlayed = beatmapDto.LastPlayed,
                            BeatDivisor = beatmapDto.BeatDivisor,
                            EditorTimestamp = beatmapDto.EditorTimestamp,
                        };

                        set.Beatmaps.Add(beatmap);
                    }

                    r.Add(set);
                    added++;
                }

                return added;
            });

        private static int writeScores(RealmInstance realm, IReadOnlyList<OfficialScoreDto> scores) =>
            realm.Write(r =>
            {
                int added = 0;

                foreach (var scoreDto in scores)
                {
                    if (r.Find<ScoreInfo>(scoreDto.ID) != null)
                        continue;

                    var score = new ScoreInfo
                    {
                        ID = scoreDto.ID,
                        BeatmapHash = scoreDto.BeatmapHash,
                        Ruleset = resolveRuleset(r, scoreDto.RulesetShortName),
                        ClientVersion = scoreDto.ClientVersion,
                        Hash = scoreDto.Hash,
                        DeletePending = scoreDto.DeletePending,
                        TotalScore = scoreDto.TotalScore,
                        TotalScoreWithoutMods = scoreDto.TotalScoreWithoutMods,
                        TotalScoreVersion = scoreDto.TotalScoreVersion,
                        LegacyTotalScore = scoreDto.LegacyTotalScore,
                        BackgroundReprocessingFailed = scoreDto.BackgroundReprocessingFailed,
                        MaxCombo = scoreDto.MaxCombo,
                        Accuracy = scoreDto.Accuracy,
                        Date = scoreDto.Date,
                        PP = scoreDto.PP,
                        OnlineID = scoreDto.OnlineID,
                        LegacyOnlineID = scoreDto.LegacyOnlineID,
                        RealmUser = mapUser(scoreDto.User),
                        ModsJson = scoreDto.ModsJson,
                        StatisticsJson = scoreDto.StatisticsJson,
                        MaximumStatisticsJson = scoreDto.MaximumStatisticsJson,
                        RankInt = scoreDto.RankInt,
                        Combo = scoreDto.Combo,
                        IsLegacyScore = scoreDto.IsLegacyScore,
                    };

                    if (!string.IsNullOrEmpty(scoreDto.BeatmapHash))
                        score.BeatmapInfo = r.All<BeatmapInfo>().FirstOrDefault(b => b.Hash == scoreDto.BeatmapHash);

                    foreach (int pause in scoreDto.Pauses)
                        score.Pauses.Add(pause);

                    linkFiles(r, score.Files, scoreDto.Files);
                    r.Add(score);
                    added++;
                }

                return added;
            });

        private static int writeCollections(RealmInstance realm, IReadOnlyList<OfficialCollectionDto> collections) =>
            realm.Write(r =>
            {
                int added = 0;

                foreach (var collectionDto in collections)
                {
                    if (r.Find<BeatmapCollection>(collectionDto.ID) != null)
                        continue;

                    var collection = new BeatmapCollection
                    {
                        ID = collectionDto.ID,
                        Name = collectionDto.Name,
                        LastModified = collectionDto.LastModified,
                    };

                    foreach (string md5 in collectionDto.BeatmapMD5Hashes)
                        collection.BeatmapMD5Hashes.Add(md5);

                    r.Add(collection);
                    added++;
                }

                return added;
            });

        private static RulesetInfo resolveRuleset(RealmInstance realm, string shortName)
        {
            var existing = realm.All<RulesetInfo>().FirstOrDefault(r => r.ShortName == shortName);
            if (existing != null)
                return existing;

            var created = new RulesetInfo
            {
                ShortName = shortName,
                Name = shortName,
                InstantiationInfo = string.Empty,
            };
            realm.Add(created);
            return created;
        }

        private static void linkFiles(RealmInstance realm, IList<RealmNamedFileUsage> targetFiles, IReadOnlyList<OfficialNamedFileDto> sourceFiles)
        {
            foreach (var usage in sourceFiles)
            {
                var managedFile = realm.Find<RealmFile>(usage.Hash) ?? realm.Add(new RealmFile { Hash = usage.Hash }, true);
                targetFiles.Add(new RealmNamedFileUsage { File = managedFile, Filename = usage.Filename });
            }
        }

        private static BeatmapDifficulty mapDifficulty(OfficialBeatmapDifficultyDto dto) =>
            new BeatmapDifficulty
            {
                DrainRate = dto.DrainRate,
                CircleSize = dto.CircleSize,
                OverallDifficulty = dto.OverallDifficulty,
                ApproachRate = dto.ApproachRate,
                SliderMultiplier = dto.SliderMultiplier,
                SliderTickRate = dto.SliderTickRate,
            };

        private static BeatmapMetadata mapMetadata(OfficialBeatmapMetadataDto dto)
        {
            var metadata = new BeatmapMetadata
            {
                Title = dto.Title,
                TitleUnicode = dto.TitleUnicode,
                Artist = dto.Artist,
                ArtistUnicode = dto.ArtistUnicode,
                Author = mapUser(dto.Author),
                Source = dto.Source,
                Tags = dto.Tags,
                PreviewTime = dto.PreviewTime,
                AudioFile = dto.AudioFile,
                BackgroundFile = dto.BackgroundFile,
            };

            foreach (string tag in dto.UserTags)
                metadata.UserTags.Add(tag);

            return metadata;
        }

        private static RealmUser mapUser(OfficialRealmUserDto dto) =>
            new RealmUser
            {
                OnlineID = dto.OnlineID,
                Username = dto.Username,
                CountryString = dto.CountryString,
            };
    }
}
