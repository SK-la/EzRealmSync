using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 双库 Diff：谱面集 / 难度 / 成绩 / 收藏夹 按 <see cref="Guid"/> 匹配。
    /// </summary>
    public static class RealmDiffEngine
    {
        public static ScanResult Compare(
            RealmDiffSnapshot source,
            RealmDiffSnapshot target,
            IReadOnlyList<EntityKind> entityKinds,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var sourceRows = source.EnumerateKinds(entityKinds).ToList();
            var targetRows = target.EnumerateKinds(entityKinds).ToList();

            var sourceById = sourceRows.ToDictionary(e => (e.EntityKind, e.Id));
            var targetById = targetRows.ToDictionary(e => (e.EntityKind, e.Id));

            var sourceOnly = new List<DiffItem>();
            var targetOnly = new List<DiffItem>();
            var conflicted = new List<DiffItem>();

            int total = sourceById.Count + targetById.Count;
            int processed = 0;

            void report(string message)
            {
                if (total == 0)
                {
                    progress?.Report(new ScanProgress { Progress = 1, Message = message });
                    return;
                }

                progress?.Report(new ScanProgress
                {
                    Progress = Math.Clamp(processed / (double)total, 0, 1),
                    Message = message,
                });
            }

            report("正在比对仅源有 / 不一致…");

            foreach (var (key, sourceEntity) in sourceById)
            {
                cancellationToken.ThrowIfCancellationRequested();
                processed++;

                if (!targetById.TryGetValue(key, out var targetEntity))
                {
                    sourceOnly.Add(toDiffItem(sourceEntity, DiffCategory.SourceOnly));
                    continue;
                }

                string? summary = RealmDiffConflictRules.GetConflictSummary(sourceEntity, targetEntity);
                if (summary != null)
                    conflicted.Add(toDiffItem(targetEntity, DiffCategory.Conflicted, summary));
            }

            report("正在比对仅目标有…");

            foreach (var (key, targetEntity) in targetById)
            {
                cancellationToken.ThrowIfCancellationRequested();
                processed++;

                if (!sourceById.ContainsKey(key))
                    targetOnly.Add(toDiffItem(targetEntity, DiffCategory.TargetOnly));
            }

            progress?.Report(new ScanProgress { Progress = 1, Message = "扫描完成" });

            return new ScanResult
            {
                SourceOnly = sourceOnly,
                TargetOnly = targetOnly,
                Conflicted = conflicted,
            };
        }

        private static DiffItem toDiffItem(RealmDiffEntity entity, DiffCategory category, string? conflictSummary = null) => new()
        {
            Id = entity.Id,
            Category = category,
            EntityKind = entity.EntityKind,
            Title = entity.Title,
            Artist = entity.Artist,
            Hash = entity.Hash,
            Ruleset = entity.Ruleset,
            Date = entity.Date,
            ConflictSummary = conflictSummary,
        };
    }
}
