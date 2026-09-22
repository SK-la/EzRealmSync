using System.Text.Json;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 官方客户端认识的 mod 缩写名录（<c>Realm/OfficialModList.json</c> 嵌入资源，
    /// 由 <c>scripts/Generate-OfficialModList.ps1</c> 从 osu 仓库生成）。
    ///
    /// 为什么是数据文件：产品进程不加载 <c>osu.Game.dll</c>，「这条 mod 官方认不认」只能靠名录判断；
    /// 靠 Ez 侧硬编码黑名单会误杀同名官方 mod（Ez mania 的 <c>DS</c>/<c>DP</c>/<c>GR</c>/<c>RP</c>
    /// 在官方同名 mod 上完全合法），所以只放行「官方确有此缩写」的组合。
    ///
    /// 缩写比对区分大小写：官方解析 API mod 时也区分，大小写不符的缩写官方还原不出来。
    /// </summary>
    public static class OfficialModCatalog
    {
        private static readonly Lazy<Catalog> catalog = new(load, isThreadSafe: true);

        /// <summary>跨规则集共享的 mod 缩写（<c>osu.Game/Rulesets/Mods</c>）。</summary>
        public static IReadOnlySet<string> Shared => catalog.Value.Shared;

        /// <summary>规则集短名 → 该规则集专属官方 mod 缩写（不含共享）。</summary>
        public static IReadOnlyDictionary<string, IReadOnlySet<string>> PerRuleset => catalog.Value.PerRuleset;

        /// <summary>规则集短名 + mod 缩写能否被官方客户端还原。规则集不在名录里一律视为不能。</summary>
        public static bool IsOfficial(string? rulesetShortName, string? acronym)
        {
            if (string.IsNullOrWhiteSpace(rulesetShortName) || string.IsNullOrWhiteSpace(acronym))
                return false;

            if (!PerRuleset.TryGetValue(rulesetShortName, out var acronyms))
                return false;

            return Shared.Contains(acronym) || acronyms.Contains(acronym);
        }

        private static Catalog load()
        {
            var shared = new HashSet<string>(StringComparer.Ordinal);
            var perRuleset = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);

            using Stream? stream = typeof(OfficialModCatalog).Assembly
                                                             .GetManifestResourceStream("osu.Game.EzRealmSync.Realm.OfficialModList.json");

            if (stream == null)
                throw new InvalidOperationException("嵌入资源 OfficialModList.json 缺失；请重新 build osu.Game.EzRealmSync。");

            using var document = JsonDocument.Parse(stream);

            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (property.Name.StartsWith('$') || property.Name is "generatedFrom" || property.Value.ValueKind != JsonValueKind.Array)
                    continue;

                var acronyms = new HashSet<string>(StringComparer.Ordinal);

                foreach (JsonElement item in property.Value.EnumerateArray())
                {
                    if (item.GetString() is { Length: > 0 } acronym)
                        acronyms.Add(acronym);
                }

                if (property.Name == "shared")
                    shared.UnionWith(acronyms);
                else
                    perRuleset[property.Name] = acronyms;
            }

            return new Catalog(shared, perRuleset);
        }

        private sealed record Catalog(IReadOnlySet<string> Shared, IReadOnlyDictionary<string, IReadOnlySet<string>> PerRuleset);
    }
}
