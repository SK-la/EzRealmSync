#if HAS_EZ_OSU_GAME
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;
using osu.Game.Input.Bindings;
using osu.Game.Models;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using Realms;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 同类型 schema 升级时复制核心业务表；<see cref="osu.Game.Models.RealmFile"/> / <see cref="SkinInfo"/> 由 <see cref="RealmAuxiliaryTablePreserver"/> 单独写回。
    /// </summary>
    internal static class RealmSchemaMigrationCopier
    {
        public static void CopyCoreData(
            RealmAccess sourceAccess,
            RealmAccess targetAccess,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            copyCoreDataInternal(sourceAccess, targetAccess, stripEzForOfficial: false, progress, cancellationToken);

        /// <summary>Ez→官方：按表复制并剥离 Ez 独有字段；返回写入行数合计。</summary>
        public static int CopyCoreDataEzToOfficial(
            RealmAccess sourceAccess,
            RealmAccess targetAccess,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            int total = 0;
            total += copyDetached<RulesetInfo>(sourceAccess, targetAccess, copyRulesets, stripRuleset, 0.15, "正在复制规则集…", progress, cancellationToken);
            total += copyDetached<BeatmapSetInfo>(sourceAccess, targetAccess, copyBeatmapSets, stripBeatmapSet, 0.25, "正在复制谱面集…", progress, cancellationToken);
            total += copyDetached<ScoreInfo>(sourceAccess, targetAccess, copyScores, stripScore, 0.55, "正在复制成绩…", progress, cancellationToken);
            total += copyDetached<BeatmapCollection>(sourceAccess, targetAccess, copyCollections, null, 0.72, "正在复制收藏夹…", progress, cancellationToken);

            progress?.Report(new ScanProgress { Progress = 0.95, Message = "核心数据复制完成" });
            return total;
        }

        private static int copyCoreDataInternal(
            RealmAccess sourceAccess,
            RealmAccess targetAccess,
            bool stripEzForOfficial,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            int total = 0;
            total += copyDetached<RulesetInfo>(sourceAccess, targetAccess, copyRulesets, stripEzForOfficial ? stripRuleset : null, 0.15, "正在复制规则集…", progress, cancellationToken);
            total += copyDetached<BeatmapSetInfo>(sourceAccess, targetAccess, copyBeatmapSets, stripEzForOfficial ? stripBeatmapSet : null, 0.25, "正在复制谱面集…", progress, cancellationToken);
            total += copyDetached<ScoreInfo>(sourceAccess, targetAccess, copyScores, stripEzForOfficial ? stripScore : null, 0.55, "正在复制成绩…", progress, cancellationToken);
            total += copyDetached<BeatmapCollection>(sourceAccess, targetAccess, copyCollections, null, 0.72, "正在复制收藏夹…", progress, cancellationToken);
            total += copyDetached<RealmKeyBinding>(sourceAccess, targetAccess, copyKeyBindings, null, 0.82, "正在复制键位…", progress, cancellationToken);
            total += copyDetached<ModPreset>(sourceAccess, targetAccess, copyModPresets, null, 0.88, "正在复制 Mod 预设…", progress, cancellationToken);
            total += copyDetached<RealmRulesetSetting>(sourceAccess, targetAccess, copyRulesetSettings, null, 0.92, "正在复制规则集设置…", progress, cancellationToken);

            progress?.Report(new ScanProgress { Progress = 0.95, Message = "核心数据复制完成" });
            return total;
        }

        private static void stripRuleset(RulesetInfo ruleset) => OfficialRealmMapper.StripEzOnlyRulesetFields(ruleset);

        private static void stripBeatmapSet(BeatmapSetInfo set) => OfficialRealmMapper.StripEzOnlyBeatmapSetFields(set);

        private static void stripScore(ScoreInfo score)
        {
            OfficialRealmMapper.StripEzOnlyScoreFields(score);

            if (score.Ruleset != null)
                OfficialRealmMapper.StripEzOnlyRulesetFields(score.Ruleset);
        }

        private static int copyDetached<T>(
            RealmAccess sourceAccess,
            RealmAccess targetAccess,
            Action<RealmInstance, List<T>> collect,
            Action<T>? prepareForOfficial,
            double progressStart,
            string message,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
            where T : RealmObjectBase
        {
            progress?.Report(new ScanProgress { Progress = progressStart, Message = message });

            int count = 0;

            sourceAccess.Run(source =>
            {
                var items = new List<T>();
                collect(source, items);

                for (int i = 0; i < items.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    T item = items[i];
                    prepareForOfficial?.Invoke(item);

                    targetAccess.Write(target => insertDetached(item, target));
                    count++;

                    if (items.Count > 0 && (i + 1) % 250 == 0)
                    {
                        progress?.Report(new ScanProgress
                        {
                            Progress = progressStart + ((i + 1) / (double)items.Count) * 0.08,
                            Message = $"{message} {i + 1:N0}/{items.Count:N0}",
                        });
                    }
                }

                if (items.Count > 0)
                {
                    progress?.Report(new ScanProgress
                    {
                        Progress = progressStart + 0.08,
                        Message = $"{message} {items.Count:N0}/{items.Count:N0}",
                    });
                }
            });

            return count;
        }

        private static void copyDetached<T>(
            RealmAccess sourceAccess,
            RealmAccess targetAccess,
            Action<RealmInstance, List<T>> collect,
            double progressStart,
            string message,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
            where T : RealmObjectBase =>
            copyDetached(sourceAccess, targetAccess, collect, null, progressStart, message, progress, cancellationToken);

        private static void insertDetached<T>(T item, RealmInstance target) where T : RealmObjectBase
        {
            switch (item)
            {
                case RulesetInfo ruleset:
                    if (target.All<RulesetInfo>().Any(r => r.ShortName == ruleset.ShortName))
                        return;

                    target.Add(ruleset);
                    break;

                case BeatmapSetInfo set:
                    if (target.Find<BeatmapSetInfo>(set.ID) != null)
                        return;

                    insertBeatmapSet(set, target);
                    break;

                case ScoreInfo score:
                    if (target.Find<ScoreInfo>(score.ID) != null)
                        return;

                    insertScore(score, target);
                    break;

                case BeatmapCollection collection:
                    if (target.Find<BeatmapCollection>(collection.ID) != null)
                        return;

                    target.Add(collection);
                    break;

                case RealmKeyBinding binding:
                    if (target.All<RealmKeyBinding>().Any(b => b.ID == binding.ID))
                        return;

                    target.Add(binding);
                    break;

                case ModPreset preset:
                    if (target.Find<ModPreset>(preset.ID) != null)
                        return;

                    target.Add(preset);
                    break;

                case RealmRulesetSetting setting:
                    if (target.All<RealmRulesetSetting>().Any(s => s.RulesetName == setting.RulesetName && s.Variant == setting.Variant && s.Key == setting.Key))
                        return;

                    target.Add(setting);
                    break;
            }
        }

        private static void copyRulesets(RealmInstance source, List<RulesetInfo> items)
        {
            foreach (var ruleset in source.All<RulesetInfo>())
                items.Add(ruleset.Detach());
        }

        private static void copyBeatmapSets(RealmInstance source, List<BeatmapSetInfo> items)
        {
            foreach (var set in source.LiveBeatmapSets())
                items.Add(set.Detach());
        }

        private static void copyScores(RealmInstance source, List<ScoreInfo> items)
        {
            foreach (var score in source.LiveScores())
                items.Add(score.Detach());
        }

        private static void copyCollections(RealmInstance source, List<BeatmapCollection> items)
        {
            foreach (var collection in source.All<BeatmapCollection>())
                items.Add(collection.Detach());
        }

        private static void copyKeyBindings(RealmInstance source, List<RealmKeyBinding> items)
        {
            foreach (var binding in source.All<RealmKeyBinding>())
                items.Add(binding.Detach());
        }

        private static void copyModPresets(RealmInstance source, List<ModPreset> items)
        {
            foreach (var preset in source.All<ModPreset>().Where(p => !p.DeletePending))
                items.Add(preset.Detach());
        }

        private static void copyRulesetSettings(RealmInstance source, List<RealmRulesetSetting> items)
        {
            foreach (var setting in source.All<RealmRulesetSetting>())
                items.Add(setting.Detach());
        }

        private static void insertBeatmapSet(BeatmapSetInfo detached, RealmInstance target)
        {
            linkFiles(target, detached.Files);

            foreach (var beatmap in detached.Beatmaps)
            {
                beatmap.Ruleset = resolveRuleset(target, beatmap.Ruleset);
                beatmap.BeatmapSet = detached;
            }

            target.Add(detached);
        }

        private static void insertScore(ScoreInfo detached, RealmInstance target)
        {
            detached.Ruleset = resolveRuleset(target, detached.Ruleset);

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

            target.Add(source);
            return source;
        }
    }
}
#endif
