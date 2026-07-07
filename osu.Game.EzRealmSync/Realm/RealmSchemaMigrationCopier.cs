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
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 工具侧 schema 升级：在同类型库之间复制全部行数据（Ez→Ez / 官方→官方），不触发游戏 migration 回调。
    /// </summary>
    internal static class RealmSchemaMigrationCopier
    {
        public static void CopyAll(
            RealmAccess sourceAccess,
            RealmAccess targetAccess,
            RealmDiskSchemaKind kind,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            bool preserveEzFields = kind == RealmDiskSchemaKind.EzExtended;

            sourceAccess.Run(source =>
            {
                targetAccess.Write(target =>
                {
                    progress?.Report(new ScanProgress { Progress = 0.05, Message = "正在复制文件索引…" });
                    copyRealmFiles(source, target, cancellationToken);

                    progress?.Report(new ScanProgress { Progress = 0.12, Message = "正在复制规则集…" });
                    copyRulesets(source, target, preserveEzFields, cancellationToken);

                    progress?.Report(new ScanProgress { Progress = 0.18, Message = "正在复制皮肤…" });
                    copySkins(source, target, cancellationToken);

                    progress?.Report(new ScanProgress { Progress = 0.28, Message = "正在复制谱面集…" });
                    copyBeatmapSets(source, target, preserveEzFields, cancellationToken);

                    progress?.Report(new ScanProgress { Progress = 0.62, Message = "正在复制成绩…" });
                    copyScores(source, target, preserveEzFields, cancellationToken);

                    progress?.Report(new ScanProgress { Progress = 0.78, Message = "正在复制收藏夹…" });
                    copyCollections(source, target, cancellationToken);

                    progress?.Report(new ScanProgress { Progress = 0.86, Message = "正在复制键位与 Mod 预设…" });
                    copyKeyBindings(source, target, cancellationToken);
                    copyModPresets(source, target, cancellationToken);
                    copyRulesetSettings(source, target, cancellationToken);
                });
            });

            progress?.Report(new ScanProgress { Progress = 1, Message = "数据复制完成" });
        }

        private static void copyRealmFiles(RealmInstance source, RealmInstance target, CancellationToken cancellationToken)
        {
            foreach (var file in source.All<RealmFile>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (target.Find<RealmFile>(file.Hash) == null)
                    target.Add(new RealmFile { Hash = file.Hash }, true);
            }
        }

        private static void copyRulesets(RealmInstance source, RealmInstance target, bool preserveEzFields, CancellationToken cancellationToken)
        {
            foreach (var ruleset in source.All<RulesetInfo>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (target.All<RulesetInfo>().Any(r => r.ShortName == ruleset.ShortName))
                    continue;

                var copy = new RulesetInfo(ruleset.ShortName, ruleset.Name, ruleset.InstantiationInfo, ruleset.OnlineID)
                {
                    Available = ruleset.Available,
                    LastAppliedDifficultyVersion = ruleset.LastAppliedDifficultyVersion,
                };

                if (preserveEzFields)
                    copy.LastAppliedXxySrVersion = ruleset.LastAppliedXxySrVersion;
                else
                    OfficialRealmMapper.StripEzOnlyRulesetFields(copy);

                target.Add(copy);
            }
        }

        private static void copySkins(RealmInstance source, RealmInstance target, CancellationToken cancellationToken)
        {
            foreach (var skin in source.LiveSkins())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (target.Find<SkinInfo>(skin.ID) != null)
                    continue;

                var detached = skin.Detach();
                linkFiles(target, detached.Files);
                target.Add(detached);
            }
        }

        private static void copyBeatmapSets(RealmInstance source, RealmInstance target, bool preserveEzFields, CancellationToken cancellationToken)
        {
            foreach (var set in source.LiveBeatmapSets())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (target.Find<BeatmapSetInfo>(set.ID) != null)
                    continue;

                var detached = set.Detach();

                if (preserveEzFields)
                    prepareBeatmapSetForEz(detached);
                else
                    prepareBeatmapSetForOfficial(detached);

                insertBeatmapSet(detached, target);
            }
        }

        private static void copyScores(RealmInstance source, RealmInstance target, bool preserveEzFields, CancellationToken cancellationToken)
        {
            foreach (var score in source.LiveScores())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (target.Find<ScoreInfo>(score.ID) != null)
                    continue;

                var detached = score.Detach();

                if (!preserveEzFields)
                    prepareScoreForOfficial(detached);

                insertScore(detached, target);
            }
        }

        private static void copyCollections(RealmInstance source, RealmInstance target, CancellationToken cancellationToken)
        {
            foreach (var collection in source.All<BeatmapCollection>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (target.Find<BeatmapCollection>(collection.ID) != null)
                    continue;

                target.Add(collection.Detach());
            }
        }

        private static void copyKeyBindings(RealmInstance source, RealmInstance target, CancellationToken cancellationToken)
        {
            foreach (var binding in source.All<RealmKeyBinding>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (target.All<RealmKeyBinding>().Any(b => b.ID == binding.ID))
                    continue;

                target.Add(binding.Detach());
            }
        }

        private static void copyModPresets(RealmInstance source, RealmInstance target, CancellationToken cancellationToken)
        {
            foreach (var preset in source.All<ModPreset>().Where(p => !p.DeletePending))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (target.Find<ModPreset>(preset.ID) != null)
                    continue;

                target.Add(preset.Detach());
            }
        }

        private static void copyRulesetSettings(RealmInstance source, RealmInstance target, CancellationToken cancellationToken)
        {
            foreach (var setting in source.All<RealmRulesetSetting>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (target.All<RealmRulesetSetting>().Any(s => s.RulesetName == setting.RulesetName && s.Variant == setting.Variant))
                    continue;

                target.Add(setting.Detach());
            }
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

        private static void prepareBeatmapForEz(BeatmapInfo beatmap) => OfficialRealmMapper.NormalizeEzOnlyBeatmapFields(beatmap);

        private static void prepareScoreForOfficial(ScoreInfo score)
        {
            OfficialRealmMapper.StripEzOnlyScoreFields(score);

            if (score.Ruleset != null)
                OfficialRealmMapper.StripEzOnlyRulesetFields(score.Ruleset);
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

            var added = new RulesetInfo(source.ShortName, source.Name, source.InstantiationInfo, source.OnlineID);
            target.Add(added);
            return added;
        }
    }
}
#endif
