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
using osu.Game.Skinning;
using Realms;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 同类型库 schema 升级：把源库全部行原样 detach 后写入目标库。
    /// Ez 与官方差异仅在少量 Ez 列；同类型升级不做 strip/normalize。
    /// </summary>
    internal static class RealmSchemaMigrationCopier
    {
        private const int file_hash_batch_size = 8_000;

        public static void CopyAll(
            RealmAccess sourceAccess,
            RealmAccess targetAccess,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            copyRealmFiles(sourceAccess, targetAccess, progress, cancellationToken);
            copyDetached<RulesetInfo>(sourceAccess, targetAccess, copyRulesets, 0.12, "正在复制规则集…", progress, cancellationToken);
            copyDetached<SkinInfo>(sourceAccess, targetAccess, copySkins, 0.20, "正在复制皮肤…", progress, cancellationToken);
            copyDetached<BeatmapSetInfo>(sourceAccess, targetAccess, copyBeatmapSets, 0.30, "正在复制谱面集…", progress, cancellationToken);
            copyDetached<ScoreInfo>(sourceAccess, targetAccess, copyScores, 0.62, "正在复制成绩…", progress, cancellationToken);
            copyDetached<BeatmapCollection>(sourceAccess, targetAccess, copyCollections, 0.78, "正在复制收藏夹…", progress, cancellationToken);
            copyDetached<RealmKeyBinding>(sourceAccess, targetAccess, copyKeyBindings, 0.86, "正在复制键位…", progress, cancellationToken);
            copyDetached<ModPreset>(sourceAccess, targetAccess, copyModPresets, 0.90, "正在复制 Mod 预设…", progress, cancellationToken);
            copyDetached<RealmRulesetSetting>(sourceAccess, targetAccess, copyRulesetSettings, 0.94, "正在复制规则集设置…", progress, cancellationToken);

            progress?.Report(new ScanProgress { Progress = 1, Message = "数据复制完成" });
        }

        private static void copyRealmFiles(
            RealmAccess sourceAccess,
            RealmAccess targetAccess,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            var hashes = new List<string>();

            sourceAccess.Run(source =>
            {
                foreach (var file in source.All<RealmFile>())
                    hashes.Add(file.Hash);
            });

            progress?.Report(new ScanProgress { Progress = 0.08, Message = $"正在复制文件索引（{hashes.Count:N0}）…" });

            for (int offset = 0; offset < hashes.Count; offset += file_hash_batch_size)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = hashes.Skip(offset).Take(file_hash_batch_size).ToList();
                double batchProgress = hashes.Count == 0 ? 1 : (offset + batch.Count) / (double)hashes.Count;

                targetAccess.Write(target =>
                {
                    foreach (string hash in batch)
                    {
                        if (target.Find<RealmFile>(hash) == null)
                            target.Add(new RealmFile { Hash = hash }, true);
                    }
                });

                progress?.Report(new ScanProgress
                {
                    Progress = 0.08 + batchProgress * 0.04,
                    Message = $"正在复制文件索引 {Math.Min(offset + batch.Count, hashes.Count):N0}/{hashes.Count:N0}…",
                });
            }
        }

        private static void copyDetached<T>(
            RealmAccess sourceAccess,
            RealmAccess targetAccess,
            Action<RealmInstance, List<T>> collect,
            double progressStart,
            string message,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
            where T : RealmObjectBase
        {
            progress?.Report(new ScanProgress { Progress = progressStart, Message = message });

            var items = new List<T>();
            sourceAccess.Run(source => collect(source, items));

            for (int i = 0; i < items.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                T item = items[i];

                targetAccess.Write(target => insertDetached(item, target));

                if (items.Count > 0 && (i + 1) % 250 == 0)
                {
                    progress?.Report(new ScanProgress
                    {
                        Progress = progressStart + ((i + 1) / (double)items.Count) * 0.08,
                        Message = $"{message} {i + 1:N0}/{items.Count:N0}",
                    });
                }
            }
        }

        private static void insertDetached<T>(T item, RealmInstance target) where T : RealmObjectBase
        {
            switch (item)
            {
                case RulesetInfo ruleset:
                    if (target.All<RulesetInfo>().Any(r => r.ShortName == ruleset.ShortName))
                        return;

                    target.Add(ruleset);
                    break;

                case SkinInfo skin:
                    if (target.Find<SkinInfo>(skin.ID) != null)
                        return;

                    linkFiles(target, skin.Files);
                    target.Add(skin);
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

        private static void copySkins(RealmInstance source, List<SkinInfo> items)
        {
            foreach (var skin in source.All<SkinInfo>().Where(s => !s.DeletePending))
                items.Add(skin.Detach());
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
