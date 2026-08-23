#if HAS_EZ_OSU_GAME
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;
using osu.Game.Models;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 将 Ez 库中的行复制到官方库（strip Ez 列；<see cref="RealmFile"/> 按 Hash 复用）。
    /// </summary>
    public static class RealmRowCopier
    {
        public static ApplyResult Apply(
            ApplyRequest request,
            RealmAccess sourceAccess,
            RealmAccess targetAccess,
            IProgress<ApplyProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            string? validationError = RealmApplySupport.ValidateApplyRequest(request);
            if (validationError != null)
                throw new InvalidOperationException(validationError);

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

                if (request.DeleteFromSource)
                {
                    if (tryDeleteFromSource(id, sourceAccess))
                        applied++;
                }
                else if (tryCopyToTarget(id, sourceAccess, targetAccess, stripEzFieldsForOfficial, normalizeEzFieldsForTarget))
                {
                    applied++;
                }
            }

            progress?.Report(new ApplyProgress { Progress = 1, Message = "写入完成" });

            return new ApplyResult { AppliedCount = applied };
        }

        private static bool tryCopyToTarget(Guid id, RealmAccess sourceAccess, RealmAccess targetAccess, bool stripEzFieldsForOfficial, bool normalizeEzFieldsForTarget)
        {
            bool copied = false;

            sourceAccess.Run(source =>
            {
                if (tryDetachBeatmapSet(source, id, out var set))
                {
                    if (stripEzFieldsForOfficial)
                        prepareBeatmapSetForOfficial(set);
                    else if (normalizeEzFieldsForTarget)
                        prepareBeatmapSetForEz(set);

                    targetAccess.Write(target => insertBeatmapSet(set, target));
                    copied = true;
                    return;
                }

                if (tryDetachBeatmap(source, id, out var beatmap))
                {
                    if (stripEzFieldsForOfficial)
                        prepareBeatmapForOfficial(beatmap);
                    else if (normalizeEzFieldsForTarget)
                        prepareBeatmapForEz(beatmap);

                    targetAccess.Write(target => insertBeatmap(beatmap, target));
                    copied = true;
                    return;
                }

                if (tryDetachScore(source, id, out var score))
                {
                    if (stripEzFieldsForOfficial)
                        prepareScoreForOfficial(score);

                    targetAccess.Write(target => insertScore(score, target));
                    copied = true;
                    return;
                }

                if (tryDetachCollection(source, id, out var collection))
                {
                    targetAccess.Write(target => insertCollection(collection, target));
                    copied = true;
                }
            });

            return copied;
        }

        private static bool tryDeleteFromSource(Guid id, RealmAccess sourceAccess)
        {
            bool deleted = false;

            sourceAccess.Write(source =>
            {
                if (source.Find<BeatmapSetInfo>(id) is BeatmapSetInfo set)
                {
                    set.DeletePending = true;
                    deleted = true;
                    return;
                }

                if (source.Find<ScoreInfo>(id) is ScoreInfo score)
                {
                    score.DeletePending = true;
                    deleted = true;
                    return;
                }

                if (source.Find<BeatmapCollection>(id) is BeatmapCollection collection)
                {
                    source.Remove(collection);
                    deleted = true;
                }
            });

            return deleted;
        }

        private static bool tryDetachBeatmapSet(RealmInstance source, Guid id, out BeatmapSetInfo detached)
        {
            detached = null!;
            if (source.Find<BeatmapSetInfo>(id) is not BeatmapSetInfo set || set.DeletePending)
                return false;

            detached = set.Detach();
            return true;
        }

        private static bool tryDetachBeatmap(RealmInstance source, Guid id, out BeatmapInfo detached)
        {
            detached = null!;
            if (source.Find<BeatmapInfo>(id) is not BeatmapInfo beatmap)
                return false;

            if (beatmap.BeatmapSet?.DeletePending == true)
                return false;

            detached = beatmap.Detach();
            return true;
        }

        private static bool tryDetachScore(RealmInstance source, Guid id, out ScoreInfo detached)
        {
            detached = null!;
            if (source.Find<ScoreInfo>(id) is not ScoreInfo score || score.DeletePending)
                return false;

            detached = score.Detach();
            return true;
        }

        private static bool tryDetachCollection(RealmInstance source, Guid id, out BeatmapCollection detached)
        {
            detached = null!;
            if (source.Find<BeatmapCollection>(id) is not BeatmapCollection collection)
                return false;

            detached = collection.Detach();
            return true;
        }

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

        private static void prepareBeatmapForEz(BeatmapInfo beatmap)
        {
            OfficialRealmMapper.NormalizeEzOnlyBeatmapFields(beatmap);
        }

        private static void prepareScoreForOfficial(ScoreInfo score)
        {
            OfficialRealmMapper.StripEzOnlyScoreFields(score);
            if (score.Ruleset != null)
                OfficialRealmMapper.StripEzOnlyRulesetFields(score.Ruleset);
        }

        private static void insertBeatmapSet(BeatmapSetInfo detached, RealmInstance target)
        {
            // upsert：已存在（含软删）则移除后写入，使冲突/假「仅 A」可被覆盖并复活。
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
            {
                detached.BeatmapInfo = target.All<BeatmapInfo>().FirstOrDefault(b => b.Hash == detached.BeatmapHash);
            }

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
