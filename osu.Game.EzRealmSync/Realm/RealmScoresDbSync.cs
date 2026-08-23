#if HAS_EZ_OSU_GAME
using osu.Framework.Extensions;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.EzRealmSync.IO;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// Realm 成绩 ↔ osu!stable <c>scores.db</c>。
    /// </summary>
    /// <remarks>
    /// TODO(legacy-db-merge):
    /// - 将选中成绩合并写入已有 scores.db（按 Beatmap MD5 分组追加，可选按 ReplayMd5 / OnlineId 去重）
    /// - 从 scores.db 导入回 Realm（与 collection.db → Realm 导入对称）
    /// </remarks>
    internal static class RealmScoresDbSync
    {
        public static int Export(RealmAccess access, IReadOnlyCollection<Guid> selectedIds, string outputFile)
        {
            var idSet = selectedIds as HashSet<Guid> ?? selectedIds.ToHashSet();
            var byMd5 = new Dictionary<string, List<LegacyScoresDbScore>>(StringComparer.OrdinalIgnoreCase);
            int written = 0;

            access.Run(realm =>
            {
                foreach (var score in realm.All<ScoreInfo>().Where(s => !s.DeletePending))
                {
                    if (idSet.Count > 0 && !idSet.Contains(score.ID))
                        continue;

                    if (!tryMapScore(score, out var mapped, out string beatmapMd5))
                        continue;

                    if (!byMd5.TryGetValue(beatmapMd5, out var list))
                    {
                        list = new List<LegacyScoresDbScore>();
                        byMd5[beatmapMd5] = list;
                    }

                    list.Add(mapped);
                    written++;
                }
            });

            var groups = byMd5
                         .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                         .Select(kv => new LegacyScoresDbBeatmapGroup(kv.Key, kv.Value))
                         .ToList();

            LegacyScoresDb.WriteFile(outputFile, groups);
            return written;
        }

        private static bool tryMapScore(ScoreInfo score, out LegacyScoresDbScore mapped, out string beatmapMd5)
        {
            mapped = null!;
            beatmapMd5 = string.Empty;

            if (!score.Ruleset.IsLegacyRuleset())
                return false;

            beatmapMd5 = score.BeatmapInfo?.MD5Hash ?? string.Empty;
            if (string.IsNullOrWhiteSpace(beatmapMd5))
                return false;

            int mods = 0;
            try
            {
                mods = (int)score.Ruleset.CreateInstance().ConvertToLegacyMods(score.Mods);
            }
            catch
            {
                // 规则集无法实例化时仍写出，mods=0
            }

            long onlineId = score.LegacyOnlineID != 0 && score.LegacyOnlineID != -1
                ? score.LegacyOnlineID
                : score.OnlineID;

            if (onlineId == 0)
                onlineId = -1;

            int totalScore = score.IsLegacyScore && score.LegacyTotalScore is long legacy
                ? (int)Math.Clamp(legacy, int.MinValue, int.MaxValue)
                : (int)Math.Clamp(score.TotalScore, int.MinValue, int.MaxValue);

            int version = score.TotalScoreVersion > 0
                ? score.TotalScoreVersion
                : LegacyScoresDb.DefaultVersion;

            // lazer 本地版本号超过 stable 习惯时，仍写入（stable 通常忽略未知版本字段）。
            string replayMd5 = !string.IsNullOrEmpty(score.Hash)
                ? score.Hash
                : FormattableString.Invariant($"lazer-{score.RealmUser.Username}-{score.Date}").ComputeMD5Hash();

            bool perfect;
            try
            {
                perfect = score.MaxCombo == score.GetMaximumAchievableCombo();
            }
            catch
            {
                perfect = false;
            }

            mapped = new LegacyScoresDbScore
            {
                GameplayMode = (byte)Math.Clamp(score.Ruleset.OnlineID, 0, 255),
                Version = version,
                BeatmapMd5 = beatmapMd5,
                PlayerName = score.RealmUser.Username ?? string.Empty,
                ReplayMd5 = replayMd5,
                Count300 = (ushort)(score.GetCount300() ?? 0),
                Count100 = (ushort)(score.GetCount100() ?? 0),
                Count50 = (ushort)(score.GetCount50() ?? 0),
                CountGeki = (ushort)(score.GetCountGeki() ?? 0),
                CountKatu = (ushort)(score.GetCountKatu() ?? 0),
                CountMiss = (ushort)(score.GetCountMiss() ?? 0),
                TotalScore = totalScore,
                MaxCombo = (ushort)Math.Clamp(score.MaxCombo, 0, ushort.MaxValue),
                PerfectCombo = perfect,
                Mods = mods,
                TimestampTicks = score.Date.UtcDateTime.Ticks,
                OnlineScoreId = onlineId,
            };

            return true;
        }
    }
}
#endif
