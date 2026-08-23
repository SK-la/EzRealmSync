using osu.Game.EzRealmSync.Contracts;

namespace osu.Game.EzRealmSync.OfficialSchema
{
    /// <summary>将同步 Apply 包写入已有官方镜像库（按 GUID upsert：覆盖冲突并复活软删）。</summary>
    public static class OfficialMirrorApplyImporter
    {
        public static OfficialApplyImportResult Apply(OfficialApplyImportJob job)
        {
            try
            {
                string path = Path.GetFullPath(job.TargetRealmPath);
                int schema = job.PinnedDiskSchemaVersion;
                var idSet = job.ItemIds.ToHashSet();

                var filtered = new RealmSyncApplyBundle
                {
                    BeatmapSets = job.Bundle.BeatmapSets.Where(s => idSet.Contains(s.ID)).ToList(),
                    Beatmaps = job.Bundle.Beatmaps.Where(b => idSet.Contains(b.ID)).ToList(),
                    Scores = job.Bundle.Scores.Where(s => idSet.Contains(s.ID)).ToList(),
                    Collections = job.Bundle.Collections.Where(c => idSet.Contains(c.ID)).ToList(),
                };

                // 独立难度：包进临时谱面集写入（与主进程 importer 行为对齐：仅写入已有集合关联较复杂，这里要求 Diff 选集）
                if (filtered.Beatmaps.Count > 0 && filtered.BeatmapSets.Count == 0)
                {
                    return new OfficialApplyImportResult
                    {
                        Success = false,
                        ErrorMessage = "官方库 Apply 暂不支持仅选独立难度；请选择所属谱面集。",
                    };
                }

                // 追加写入：打开已有库而非 CreateEmpty
                using var realm = OfficialMirrorRealm.OpenPinned(path, schema);

                int applied = 0;
                applied += OfficialMirrorRealmWriter.AppendBeatmapSets(realm, filtered.BeatmapSets);
                applied += OfficialMirrorRealmWriter.AppendScores(realm, filtered.Scores);
                applied += OfficialMirrorRealmWriter.AppendCollections(realm, filtered.Collections);

                return new OfficialApplyImportResult
                {
                    Success = true,
                    AppliedCount = applied,
                };
            }
            catch (Exception ex)
            {
                return new OfficialApplyImportResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                };
            }
        }
    }
}
