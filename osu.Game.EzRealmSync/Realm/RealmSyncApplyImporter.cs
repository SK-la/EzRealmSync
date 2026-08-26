#if HAS_EZ_OSU_GAME
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Models;
using osu.Game.Models;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 将 sidecar 导出的 DTO 包写入目标 <see cref="RealmAccess"/>（主 lib）。
    /// </summary>
    public static class RealmSyncApplyImporter
    {
        public static ApplyResult Apply(
            ApplyRequest request,
            RealmSyncApplyBundle bundle,
            RealmAccess targetAccess,
            IProgress<ApplyProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            string? validationError = RealmApplySupport.ValidateApplyRequest(request);
            if (validationError != null)
                throw new InvalidOperationException(validationError);

            if (request.DeleteFromSource)
                throw new InvalidOperationException("Delete 不得经 sidecar importer；请走单库删除路径。");

            bool stripEzFieldsForOfficial = request.WritePlan?.StripEzFieldsForTarget
                                            ?? request.Direction == SyncDirection.EzToOfficial;
            bool normalizeEzFieldsForTarget = request.WritePlan?.NormalizeEzFieldsForTarget
                                              ?? request.Direction == SyncDirection.OfficialToEz;

            int applied = 0;
            int total = request.ItemIds.Count;

            for (int i = 0; i < request.ItemIds.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Guid id = request.ItemIds[i];

                progress?.Report(new ApplyProgress
                {
                    Progress = total == 0 ? 1 : (i + 1) / (double)total,
                    Message = $"正在处理 {i + 1}/{total}…",
                });

                if (tryApplyId(id, bundle, targetAccess, stripEzFieldsForOfficial, normalizeEzFieldsForTarget))
                    applied++;
            }

            progress?.Report(new ApplyProgress { Progress = 1, Message = "写入完成" });
            return new ApplyResult { AppliedCount = applied };
        }

        private static bool tryApplyId(
            Guid id,
            RealmSyncApplyBundle bundle,
            RealmAccess targetAccess,
            bool stripEzFieldsForOfficial,
            bool normalizeEzFieldsForTarget)
        {
            var setDto = bundle.BeatmapSets.FirstOrDefault(s => s.ID == id);
            if (setDto != null)
            {
                var detached = createBeatmapSet(setDto);
                if (stripEzFieldsForOfficial)
                    prepareBeatmapSetForOfficial(detached);
                else if (normalizeEzFieldsForTarget)
                    prepareBeatmapSetForEz(detached);

                targetAccess.Write(target => insertBeatmapSet(detached, target));
                return true;
            }

            var beatmapDto = bundle.Beatmaps.FirstOrDefault(b => b.ID == id);
            if (beatmapDto != null)
            {
                var detached = createBeatmap(beatmapDto);
                if (stripEzFieldsForOfficial)
                    prepareBeatmapForOfficial(detached);
                else if (normalizeEzFieldsForTarget)
                    prepareBeatmapForEz(detached);

                targetAccess.Write(target => insertBeatmap(detached, target));
                return true;
            }

            var scoreDto = bundle.Scores.FirstOrDefault(s => s.ID == id);
            if (scoreDto != null)
            {
                var detached = createScore(scoreDto);
                if (stripEzFieldsForOfficial)
                    prepareScoreForOfficial(detached);

                targetAccess.Write(target => insertScore(detached, target));
                return true;
            }

            var collectionDto = bundle.Collections.FirstOrDefault(c => c.ID == id);
            if (collectionDto != null)
            {
                var detached = createCollection(collectionDto);
                targetAccess.Write(target => insertCollection(detached, target));
                return true;
            }

            return false;
        }

        private static BeatmapSetInfo createBeatmapSet(OfficialBeatmapSetDto dto)
        {
            var set = new BeatmapSetInfo
            {
                ID = dto.ID,
                OnlineID = dto.OnlineID,
                DateAdded = dto.DateAdded,
                DateSubmitted = dto.DateSubmitted,
                DateRanked = dto.DateRanked,
                StatusInt = dto.StatusInt,
                DeletePending = dto.DeletePending,
                Hash = dto.Hash,
                Protected = dto.Protected,
            };

            foreach (var file in dto.Files)
                set.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = file.Hash }, file.Filename));

            foreach (var beatmapDto in dto.Beatmaps)
                set.Beatmaps.Add(createBeatmap(beatmapDto));

            return set;
        }

        private static BeatmapInfo createBeatmap(OfficialBeatmapDto dto)
        {
            var beatmap = new BeatmapInfo
            {
                ID = dto.ID,
                DifficultyName = dto.DifficultyName,
                Ruleset = new RulesetInfo(dto.RulesetShortName, dto.RulesetShortName, string.Empty, 0),
                StatusInt = dto.StatusInt,
                OnlineID = dto.OnlineID,
                Length = dto.Length,
                BPM = dto.BPM,
                Hash = dto.Hash,
                StarRating = dto.StarRating,
                MD5Hash = dto.MD5Hash,
                OnlineMD5Hash = dto.OnlineMD5Hash,
                LastLocalUpdate = dto.LastLocalUpdate,
                LastOnlineUpdate = dto.LastOnlineUpdate,
                Hidden = dto.Hidden,
                EndTimeObjectCount = dto.EndTimeObjectCount,
                TotalObjectCount = dto.TotalObjectCount,
                LastPlayed = dto.LastPlayed,
                BeatDivisor = dto.BeatDivisor,
                EditorTimestamp = dto.EditorTimestamp,
                Metadata = createMetadata(dto.Metadata),
            };

            beatmap.Difficulty.DrainRate = dto.Difficulty.DrainRate;
            beatmap.Difficulty.CircleSize = dto.Difficulty.CircleSize;
            beatmap.Difficulty.OverallDifficulty = dto.Difficulty.OverallDifficulty;
            beatmap.Difficulty.ApproachRate = dto.Difficulty.ApproachRate;
            beatmap.Difficulty.SliderMultiplier = dto.Difficulty.SliderMultiplier;
            beatmap.Difficulty.SliderTickRate = dto.Difficulty.SliderTickRate;
            beatmap.UserSettings.Offset = dto.UserSettings.Offset;

            return beatmap;
        }

        private static BeatmapMetadata createMetadata(OfficialBeatmapMetadataDto dto)
        {
            var metadata = new BeatmapMetadata
            {
                Title = dto.Title,
                TitleUnicode = dto.TitleUnicode,
                Artist = dto.Artist,
                ArtistUnicode = dto.ArtistUnicode,
                Author = createUser(dto.Author),
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

        private static ScoreInfo createScore(OfficialScoreDto dto)
        {
            var score = new ScoreInfo
            {
                ID = dto.ID,
                BeatmapHash = dto.BeatmapHash,
                Ruleset = new RulesetInfo(dto.RulesetShortName, dto.RulesetShortName, string.Empty, 0),
                ClientVersion = dto.ClientVersion,
                Hash = dto.Hash,
                DeletePending = dto.DeletePending,
                TotalScore = dto.TotalScore,
                TotalScoreWithoutMods = dto.TotalScoreWithoutMods,
                TotalScoreVersion = dto.TotalScoreVersion,
                LegacyTotalScore = dto.LegacyTotalScore,
                BackgroundReprocessingFailed = dto.BackgroundReprocessingFailed,
                MaxCombo = dto.MaxCombo,
                Accuracy = dto.Accuracy,
                Date = dto.Date,
                PP = dto.PP,
                OnlineID = dto.OnlineID,
                LegacyOnlineID = dto.LegacyOnlineID,
                RealmUser = createUser(dto.User),
                ModsJson = dto.ModsJson,
                StatisticsJson = dto.StatisticsJson,
                MaximumStatisticsJson = dto.MaximumStatisticsJson,
                RankInt = dto.RankInt,
                Combo = dto.Combo,
                IsLegacyScore = dto.IsLegacyScore,
            };

            foreach (int pause in dto.Pauses)
                score.Pauses.Add(pause);

            foreach (var file in dto.Files)
                score.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = file.Hash }, file.Filename));

            return score;
        }

        private static BeatmapCollection createCollection(OfficialCollectionDto dto)
        {
            var collection = new BeatmapCollection
            {
                ID = dto.ID,
                Name = dto.Name,
                LastModified = dto.LastModified,
            };

            foreach (string md5 in dto.BeatmapMD5Hashes)
                collection.BeatmapMD5Hashes.Add(md5);

            return collection;
        }

        private static RealmUser createUser(OfficialRealmUserDto dto) =>
            new RealmUser
            {
                OnlineID = dto.OnlineID,
                Username = dto.Username,
                CountryString = dto.CountryString,
            };

        private static void prepareBeatmapSetForOfficial(BeatmapSetInfo detached)
        {
            foreach (var beatmap in detached.Beatmaps)
                prepareBeatmapForOfficial(beatmap);
        }

        private static void prepareBeatmapSetForEz(BeatmapSetInfo detached)
        {
            foreach (var beatmap in detached.Beatmaps)
                prepareBeatmapForEz(beatmap);
        }

        private static void prepareBeatmapForOfficial(BeatmapInfo beatmap)
        {
            OfficialRealmMapper.StripEzOnlyBeatmapFields(beatmap);
            OfficialRealmMapper.StripEzOnlyRulesetFields(beatmap.Ruleset);
        }

        private static void prepareBeatmapForEz(BeatmapInfo beatmap) =>
            OfficialRealmMapper.NormalizeEzOnlyBeatmapFields(beatmap);

        private static void prepareScoreForOfficial(ScoreInfo score)
        {
            OfficialRealmMapper.StripEzOnlyScoreFields(score);
            if (score.Ruleset != null)
                OfficialRealmMapper.StripEzOnlyRulesetFields(score.Ruleset);
        }

        private static void insertBeatmapSet(BeatmapSetInfo detached, RealmInstance target)
        {
            if (target.Find<BeatmapSetInfo>(detached.ID) is BeatmapSetInfo existing)
                target.Remove(existing);

            linkFiles(target, detached.Files);

            foreach (var beatmap in detached.Beatmaps)
            {
                if (target.Find<BeatmapInfo>(beatmap.ID) is BeatmapInfo existingBeatmap)
                    target.Remove(existingBeatmap);

                beatmap.Ruleset = resolveRuleset(target, beatmap.Ruleset);
                beatmap.BeatmapSet = detached;
            }

            detached.DeletePending = false;
            target.Add(detached);
        }

        private static void insertBeatmap(BeatmapInfo detached, RealmInstance target)
        {
            Guid setId = detached.BeatmapSet?.ID ?? Guid.Empty;
            if (setId == Guid.Empty)
                throw new InvalidOperationException("难度缺少所属谱面集，请先同步谱面集。");

            var managedSet = target.Find<BeatmapSetInfo>(setId)
                             ?? throw new InvalidOperationException("目标库中不存在对应谱面集，请先同步谱面集。");

            if (target.Find<BeatmapInfo>(detached.ID) is BeatmapInfo existing)
                target.Remove(existing);

            detached.BeatmapSet = managedSet;
            detached.Ruleset = resolveRuleset(target, detached.Ruleset);
            target.Add(detached);
        }

        private static void insertCollection(BeatmapCollection detached, RealmInstance target)
        {
            if (target.Find<BeatmapCollection>(detached.ID) is BeatmapCollection existing)
                target.Remove(existing);

            target.Add(detached);
        }

        private static void insertScore(ScoreInfo detached, RealmInstance target)
        {
            if (target.Find<ScoreInfo>(detached.ID) is ScoreInfo existing)
                target.Remove(existing);

            detached.Ruleset = resolveRuleset(target, detached.Ruleset);
            detached.DeletePending = false;

            if (!string.IsNullOrEmpty(detached.BeatmapHash))
                detached.BeatmapInfo = target.All<BeatmapInfo>().FirstOrDefault(b => b.Hash == detached.BeatmapHash);

            linkFiles(target, detached.Files);
            target.Add(detached);
        }

        private static void linkFiles(RealmInstance target, IList<RealmNamedFileUsage> files)
        {
            for (int i = 0; i < files.Count; i++)
            {
                var usage = files[i];
                string hash = usage.File.Hash;
                var managedFile = target.Find<RealmFile>(hash) ?? target.Add(new RealmFile { Hash = hash }, true);
                files[i] = new RealmNamedFileUsage(managedFile, usage.Filename);
            }
        }

        private static RulesetInfo resolveRuleset(RealmInstance target, RulesetInfo source)
        {
            var existing = target.All<RulesetInfo>().FirstOrDefault(r => r.ShortName == source.ShortName);
            if (existing != null)
                return existing;

            var added = new RulesetInfo(source.ShortName, source.Name, source.InstantiationInfo, source.OnlineID);
            target.Add(added);
            return added;
        }
    }
}
#endif
