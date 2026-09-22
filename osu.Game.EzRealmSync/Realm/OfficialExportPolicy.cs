using System.Text.Json;
using osu.Game.EzRealmSync.Realm.Dynamic;
using Realms;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 「这一行官方客户端能不能用」的判定集合，供「转回官方版」收窄与 Ez → 官方 同步共用。
    ///
    /// 口径是**能少不能错**：官方还原不了的成绩 / 谱面宁可留在这边，也不写过去变成官方端看不见或
    /// 语义错误的行（用户已确认成绩互导不追求完整，不支持的直接过滤）。
    ///
    /// 判定全是纯数据（Ez 列存在与否、字符串、mod 名录），不依赖 <c>osu.Game.dll</c>。
    /// 注意本类不做 DB 查询，调用方把自己扫到的集合传进来。
    /// </summary>
    public static class OfficialExportPolicy
    {
        /// <summary>官方客户端有的规则集短名。</summary>
        private static readonly HashSet<string> official_rulesets = new(StringComparer.Ordinal)
        {
            "osu",
            "taiko",
            "catch",
            "mania",
        };

        /// <summary>
        /// 规则集行：Ez 独有规则集（diva / bms）、以及任何指向它们的实例化信息一律不搬。
        /// 反过来，只认官方四规则集：将来 Ez 侧再加私有规则集时，这里不必改（默认拒绝）。
        /// </summary>
        public static bool IsOfficialRuleset(string? shortName, string? instantiationInfo)
        {
            if (string.IsNullOrWhiteSpace(shortName))
                return false;

            if (OfficialBaselineSchema.IsEzOnlyRuleset(shortName))
                return false;

            if (mentionsEzOnlyRuleset(instantiationInfo))
                return false;

            // 允许短名不在官方四规则集里，但实例化信息必须指向官方规则集程序集：
            // 目标库已有的、官方能实例化的行不该被误杀。
            if (official_rulesets.Contains(shortName))
                return true;

            return !string.IsNullOrWhiteSpace(instantiationInfo)
                   && instantiationInfo.Contains("osu.Game.Rulesets.", StringComparison.Ordinal);
        }

        /// <summary>
        /// 谱面集行：外部托管的谱面集不搬（内容在 Ez 的磁盘路径上，官方读不到），
        /// 且必须至少有一条「非隐藏、规则集官方」的难度——只含 Ez 规则集难度的集搬过去等于空集。
        /// <paramref name="beatmaps"/> 为该集的难度行（调用方扫好传入）。
        /// </summary>
        public static bool IsOfficialBeatmapSet(int hostingKind, IEnumerable<BeatmapRowFacts> beatmaps)
        {
            // BeatmapSetHostingKind.External = 1：内容在 Ez 的外部目录里，官方只认 files/。
            if (hostingKind == 1)
                return false;

            return beatmaps.Any(b => !b.Hidden && b.RulesetIsOfficial);
        }

        /// <summary>难度行：隐藏的、规则集官方的判定由调用方解析后传入。</summary>
        public static bool IsOfficialBeatmap(bool hidden, bool rulesetIsOfficial, bool parentSetIsOfficial) =>
            !hidden && rulesetIsOfficial && parentSetIsOfficial;

        /// <summary>
        /// 成绩行：官方能还原的前提是「官方规则集 + 双 Lazer 判定/血条语义 + mod 全在官方名录里」。
        ///
        /// 判定模式取客户端自己的口径（<c>ManiaHitMode &gt; Lazer</c> 即非官方语义）：负数只出现在
        /// Ez v1 迁移前写入的老成绩上，客户端按 Lazer 读，因此不算「非官方」，不在这里被清掉。
        /// </summary>
        public static bool IsOfficialScore(string? rulesetShortName, int maniaHitMode, int maniaHealthMode, string? modsJson)
        {
            if (!IsOfficialRuleset(rulesetShortName, null))
                return false;

            if (isNonLazerMode(maniaHitMode) || isNonLazerMode(maniaHealthMode))
                return false;

            return areModsOfficial(rulesetShortName!, modsJson);
        }

        /// <summary>成绩 mods 列（APIMod JSON 数组）里的缩写是否全部为官方名录可还原。</summary>
        public static bool areModsOfficial(string rulesetShortName, string? modsJson)
        {
            if (string.IsNullOrWhiteSpace(modsJson))
                return true;

            string[]? acronyms = tryReadModAcronyms(modsJson);

            // 解析不出来就不能保证官方能还原，宁可不过滤错也要拦下。
            if (acronyms == null)
                return false;

            return acronyms.All(acronym => OfficialModCatalog.IsOfficial(rulesetShortName, acronym));
        }

        /// <summary>Ez 侧「非官方判定语义」的口径：<c>&gt; Lazer(0)</c>。</summary>
        private static bool isNonLazerMode(int mode) => mode > 0;

        private static bool mentionsEzOnlyRuleset(string? instantiationInfo) =>
            !string.IsNullOrWhiteSpace(instantiationInfo)
            && (instantiationInfo.Contains("Rulesets.Diva", StringComparison.OrdinalIgnoreCase)
                || instantiationInfo.Contains("Rulesets.BMS", StringComparison.OrdinalIgnoreCase));

        private static string[]? tryReadModAcronyms(string modsJson)
        {
            try
            {
                using var document = JsonDocument.Parse(modsJson);

                if (document.RootElement.ValueKind != JsonValueKind.Array)
                    return null;

                var acronyms = new List<string>();

                foreach (JsonElement element in document.RootElement.EnumerateArray())
                {
                    if (element.ValueKind != JsonValueKind.Object)
                        return null;

                    if (readStringProperty(element, "acronym") is not { Length: > 0 } value)
                        return null;

                    acronyms.Add(value);
                }

                return acronyms.ToArray();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// 按名取字符串属性，忽略大小写：客户端把 mods 存成 <c>[{"acronym":"HD"}]</c>（小写），
        /// Newtonsoft 读回时也忽略大小写，这里必须同样宽容，否则会把正常成绩当成「解析不了」丢掉。
        /// </summary>
        private static string? readStringProperty(JsonElement element, string name)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    continue;

                return property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
            }

            return null;
        }
    }

    /// <summary>判定谱面集时需要的难度事实：调用方解析好的、策略不关心的两个布尔。</summary>
    public readonly record struct BeatmapRowFacts(bool Hidden, bool RulesetIsOfficial);

    /// <summary>
    /// 搬运过程中「这一行要不要搬」的判据。给 <see cref="DynamicRealmCopier"/> 用：
    /// 被拒的行不在目标库建行，指向它的链接自然留空（copier 已有该分支）。
    /// </summary>
    public interface IDynamicCopyFilter
    {
        bool ShouldCopy(string className, IRealmObjectBase sourceRow);
    }
}
