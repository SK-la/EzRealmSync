using System.Globalization;
using System.Text.Json;
using osu.Framework.Extensions;
using osu.Game.EzRealmSync.IO;
using Realms;

namespace osu.Game.EzRealmSync.Realm.Dynamic
{
    /// <summary>
    /// Realm 成绩 → osu!stable <c>scores.db</c> 的**动态**导出。
    ///
    /// 与 typed 版结果的差异只允许来自"读不到"，不允许来自"猜"：
    /// 判定数与连击上限从库里的 JSON 列算，mods 位从 <c>ModsJson</c> 的缩写映射，
    /// 两者都是官方写进库里的既有数据。
    /// </summary>
    /// <remarks>
    /// mods 位映射表是**手抄的 stable 文件格式常量**（见 <see cref="legacy_mobs"/>）。
    /// 工具刻意不加载 osu.Game.dll，所以拿不到 <c>LegacyMods</c> 枚举；这些位值属于 stable 存档格式，
    /// 不会变。上游要是新增了"可映射到 legacy 位"的 mod，这里要跟着补。
    /// </remarks>
    internal static class DynamicScoresDbExporter
    {
        public static int Export(
            DynamicRealmSession session,
            RealmSchemaSnapshot schema,
            IReadOnlyCollection<Guid> selectedIds,
            string outputFile)
        {
            var idSet = selectedIds as HashSet<Guid> ?? selectedIds.ToHashSet();
            var byMd5 = new Dictionary<string, List<LegacyScoresDbScore>>(StringComparer.OrdinalIgnoreCase);
            int written = 0;

            foreach (IRealmObjectBase score in DynamicRowAccess.LiveRows(session, schema, OfficialBaselineSchema.Score))
            {
                if (idSet.Count > 0 && (DynamicRowAccess.Resolve(score, schema, "ID") is not Guid id || !idSet.Contains(id)))
                    continue;

                if (!tryMapScore(score, schema, out LegacyScoresDbScore mapped, out string beatmapMd5))
                    continue;

                if (!byMd5.TryGetValue(beatmapMd5, out var list))
                {
                    list = new List<LegacyScoresDbScore>();
                    byMd5[beatmapMd5] = list;
                }

                list.Add(mapped);
                written++;
            }

            var groups = byMd5
                         .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                         .Select(kv => new LegacyScoresDbBeatmapGroup(kv.Key, kv.Value))
                         .ToList();

            LegacyScoresDb.WriteFile(outputFile, groups);
            return written;
        }

        private static bool tryMapScore(IRealmObjectBase score, RealmSchemaSnapshot schema, out LegacyScoresDbScore mapped, out string beatmapMd5)
        {
            mapped = null!;
            beatmapMd5 = string.Empty;

            // scores.db 只有官方四模式的写法；Ez 专属规则集没有对应的 GameplayMode 值。
            long? rulesetOnlineId = DynamicRowAccess.ResolveLong(score, schema, "Ruleset.OnlineID");
            if (rulesetOnlineId is not (>= 0 and <= 3))
                return false;

            byte gameplayMode = (byte)rulesetOnlineId.Value;

            beatmapMd5 = DynamicRowAccess.ResolveString(score, schema, "BeatmapInfo.MD5Hash") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(beatmapMd5))
                return false;

            long legacyOnlineId = DynamicRowAccess.ResolveLong(score, schema, "LegacyOnlineID") ?? -1;
            long onlineIdValue = DynamicRowAccess.ResolveLong(score, schema, "OnlineID") ?? 0;
            long onlineScoreId = legacyOnlineId != 0 && legacyOnlineId != -1 ? legacyOnlineId : onlineIdValue;

            if (onlineScoreId == 0)
                onlineScoreId = -1;

            bool isLegacyScore = DynamicRowAccess.Resolve(score, schema, "IsLegacyScore") is true;
            long? legacyTotalScore = DynamicRowAccess.ResolveLong(score, schema, "LegacyTotalScore");
            long totalScore = DynamicRowAccess.ResolveLong(score, schema, "TotalScore") ?? 0;

            int mappedTotalScore = isLegacyScore && legacyTotalScore is { } legacy
                ? (int)Math.Clamp(legacy, int.MinValue, int.MaxValue)
                : (int)Math.Clamp(totalScore, int.MinValue, int.MaxValue);

            long totalScoreVersion = DynamicRowAccess.ResolveLong(score, schema, "TotalScoreVersion") ?? 0;

            string playerName = DynamicRowAccess.ResolveString(score, schema, "User.Username") ?? string.Empty;
            DateTimeOffset date = DynamicRowAccess.ResolveDate(score, schema, "Date") ?? default;

            string hash = DynamicRowAccess.ResolveString(score, schema, "Hash") ?? string.Empty;
            string replayMd5 = !string.IsNullOrEmpty(hash)
                ? hash
                // 与 typed 版同一串：哈希输入必须在 Invariant 下格式化，否则中文/德语系统的日期格式会算出不同 MD5。
                : FormattableString.Invariant($"lazer-{playerName}-{date.ToString(CultureInfo.InvariantCulture)}").ComputeMD5Hash();

            var statistics = readStatistics(score, schema, "Statistics");
            var maximumStatistics = readStatistics(score, schema, "MaximumStatistics");

            int maximumAchievableCombo = maximumStatistics
                                         .Where(kv => affects_combo.Contains(kv.Key))
                                         .Sum(kv => kv.Value);

            int maxCombo = (int)Math.Clamp(DynamicRowAccess.ResolveLong(score, schema, "MaxCombo") ?? 0, int.MinValue, int.MaxValue);

            mapped = new LegacyScoresDbScore
            {
                GameplayMode = gameplayMode,
                Version = totalScoreVersion > 0 ? (int)totalScoreVersion : LegacyScoresDb.DefaultVersion,
                BeatmapMd5 = beatmapMd5,
                PlayerName = playerName,
                ReplayMd5 = replayMd5,
                Count300 = toUshort(getCount(statistics, gameplayMode, "300")),
                Count100 = toUshort(getCount(statistics, gameplayMode, "100")),
                Count50 = toUshort(getCount(statistics, gameplayMode, "50")),
                CountGeki = toUshort(getCount(statistics, gameplayMode, "geki")),
                CountKatu = toUshort(getCount(statistics, gameplayMode, "katu")),
                CountMiss = toUshort(getCount(statistics, gameplayMode, "miss")),
                TotalScore = mappedTotalScore,
                MaxCombo = (ushort)Math.Clamp(maxCombo, 0, ushort.MaxValue),
                PerfectCombo = maxCombo == maximumAchievableCombo,
                Mods = convertToLegacyMods(DynamicRowAccess.ResolveString(score, schema, "Mods"), gameplayMode),
                TimestampTicks = date.UtcDateTime.Ticks,
                OnlineScoreId = onlineScoreId,
            };

            return true;
        }

        /// <summary>
        /// 判定数按官方 <c>ScoreInfoExtensions.GetCount*</c> 的模式分派复刻。返回 null 表示该模式没有这个判定项
        /// （会写成 0），不要拿别的判定凑数。
        /// </summary>
        private static int? getCount(IReadOnlyDictionary<string, int> statistics, byte rulesetOnlineId, string kind)
        {
            switch (kind)
            {
                case "300":
                    return lookup("Great");

                case "100":
                    return rulesetOnlineId == 2 ? lookup("LargeTickHit") : lookup("Ok");

                case "50":
                    return rulesetOnlineId switch
                    {
                        2 => lookup("SmallTickHit"),
                        0 or 3 => lookup("Meh"),
                        _ => null,
                    };

                case "miss":
                    return rulesetOnlineId == 2
                        ? (lookup("Miss") ?? 0) + (lookup("LargeTickMiss") ?? 0)
                        : lookup("Miss");

                case "geki":
                    return rulesetOnlineId switch
                    {
                        1 => lookup("LargeBonus"),
                        3 => lookup("Perfect"),
                        _ => null,
                    };

                case "katu":
                    return rulesetOnlineId switch
                    {
                        2 => lookup("SmallTickMiss"),
                        3 => lookup("Good"),
                        _ => null,
                    };

                default:
                    return null;
            }

            int? lookup(string hitResult) => statistics.TryGetValue(hitResult, out int value) ? value : null;
        }

        /// <summary>
        /// 读 <c>StatisticsJson</c> / <c>MaximumStatisticsJson</c>。
        /// 键是 HitResult 名字（官方用 <c>JsonConvert</c> 序列化枚举键），但历史数据可能来自
        /// <c>EnumMember</c> 形式（<c>small_tick_hit</c>），所以比对时归一化大小写与下划线。
        /// </summary>
        private static IReadOnlyDictionary<string, int> readStatistics(IRealmObjectBase score, RealmSchemaSnapshot schema, string jsonColumn)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);

            if (DynamicRowAccess.ResolveString(score, schema, jsonColumn) is not { Length: > 0 } json)
                return result;

            try
            {
                var raw = JsonSerializer.Deserialize<Dictionary<string, int>>(json);

                if (raw == null)
                    return result;

                foreach (var (key, value) in raw)
                {
                    if (normaliseHitResult(key) is { } name)
                        result[name] = value;
                }
            }
            catch (JsonException)
            {
                // 坏 JSON 按"没有判定数据"处理：导出仍写出该成绩，只是判定数为 0。
            }

            return result;
        }

        private static string? normaliseHitResult(string key)
        {
            string normalised = key.Replace("_", string.Empty);

            foreach (string known in known_hit_results)
            {
                if (string.Equals(known, normalised, StringComparison.OrdinalIgnoreCase))
                    return known;
            }

            return null;
        }

        /// <summary>与官方 <c>(ushort)(… ?? 0)</c> 一致：超范围按位截断，不做钳制（钳制会悄悄改变 stable 里的数）。</summary>
        private static ushort toUshort(int? value) => unchecked((ushort)(value ?? 0));

        /// <summary>
        /// 复刻官方 <c>Ruleset.ConvertToLegacyMods</c>（含 osu / mania 的覆写）。位值是 stable 存档格式常量。
        /// 入参是 <c>Score</c> 表的 <c>Mods</c> 列（官方模型里叫 <c>ModsJson</c>，列名不是）。
        /// </summary>
        private static int convertToLegacyMods(string? modsJson, byte rulesetOnlineId)
        {
            int value = 0;
            bool fadeIn = false;

            foreach (string acronym in readModAcronyms(modsJson))
            {
                value |= baseBits(acronym);

                switch (rulesetOnlineId)
                {
                    case 0:
                        value |= osuBits(acronym);
                        break;

                    case 3:
                        value |= maniaBits(acronym);
                        fadeIn |= acronym == "FI";
                        break;
                }
            }

            // FadeIn 是 ManiaModHidden 的派生类，官方 mania 覆写在遍历时把 Hidden 位清掉。
            // 放在最后清，避免依赖 mods JSON 里的先后顺序。
            if (fadeIn)
                value &= ~(1 << 3);

            return value;
        }

        private static int baseBits(string acronym) => acronym switch
        {
            "NF" => 1 << 0,
            "EZ" => 1 << 1,
            "HD" => 1 << 3,
            "HR" => 1 << 4,
            "PF" => (1 << 14) | (1 << 5),
            "SD" => 1 << 5,
            "NC" => (1 << 9) | (1 << 6),
            "DT" => 1 << 6,
            "RX" => 1 << 7,
            "HT" => 1 << 8,
            "FL" => 1 << 10,
            "CN" => (1 << 22) | (1 << 11),
            "AT" => 1 << 11,
            "SV2" => 1 << 29,
            _ => 0,
        };

        private static int osuBits(string acronym) => acronym switch
        {
            "AP" => 1 << 13,
            "SO" => 1 << 12,
            "TP" => 1 << 23,
            "TD" => 1 << 2,
            _ => 0,
        };

        /// <summary>
        /// mania 的键数 bit。FadeIn 清 Hidden 位由调用方在最后统一处理（避免依赖 JSON 里的 mod 顺序）。
        /// </summary>
        private static int maniaBits(string acronym) => acronym switch
        {
            "1K" => 1 << 26,
            "2K" => 1 << 28,
            "3K" => 1 << 27,
            "4K" => 1 << 15,
            "5K" => 1 << 16,
            "6K" => 1 << 17,
            "7K" => 1 << 18,
            "8K" => 1 << 19,
            "9K" => 1 << 24,
            "DS" => 1 << 25,
            "MR" => 1 << 30,
            "RD" => 1 << 21,
            "FI" => 1 << 20,
            _ => 0,
        };

        private static IEnumerable<string> readModAcronyms(string? modsJson)
        {
            if (string.IsNullOrWhiteSpace(modsJson))
                yield break;

            JsonDocument document;

            try
            {
                document = JsonDocument.Parse(modsJson);
            }
            catch (JsonException)
            {
                yield break;
            }

            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                    yield break;

                foreach (JsonElement element in document.RootElement.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.Object
                        && element.TryGetProperty("acronym", out JsonElement acronym)
                        && acronym.ValueKind == JsonValueKind.String)
                    {
                        yield return acronym.GetString() ?? string.Empty;
                    }
                }
            }
        }

        /// <summary>HitResult 里会影响连击的判定（官方 <c>HitResult.AffectsCombo</c>）；连击上限只累加这些。</summary>
        private static readonly HashSet<string> affects_combo = new HashSet<string>(StringComparer.Ordinal)
        {
            "Miss", "Meh", "Ok", "Good", "Great", "Perfect",
            "LargeTickHit", "LargeTickMiss", "LegacyComboIncrease", "ComboBreak", "SliderTailHit",
        };

        private static readonly string[] known_hit_results =
        [
            "None", "Poor", "Miss", "Meh", "Ok", "Good", "Great", "Perfect",
            "SmallTickMiss", "SmallTickHit", "LargeTickMiss", "LargeTickHit",
            "LargeBonus", "SmallBonus", "SliderTailHit", "LegacyComboIncrease", "ComboBreak", "IgnoreMiss",
        ];
    }
}
