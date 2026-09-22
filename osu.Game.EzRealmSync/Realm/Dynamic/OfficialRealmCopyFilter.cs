using Realms;

namespace osu.Game.EzRealmSync.Realm.Dynamic
{
    /// <summary>
    /// 「转回官方版」收窄时的行过滤：只搬官方客户端能用的行，其余留在备份里。
    /// 判定规则集中在 <see cref="OfficialExportPolicy"/>（纯数据），这里负责把它所需的跨界事实
    /// （规则集是否官方、集的难度构成、最终留下的难度 Hash）在源库上先算清楚。
    ///
    /// 为什么先建索引而不是逐行现查：搬运按目标 schema 的类顺序走，而谱面集要等它的难度、难度要等
    /// 它所属的集、成绩要等难度是否留下——就地现查会读到一半的事实。索引只读源库一遍，之后判定是纯内存。
    ///
    /// 不搬的行不在目标库建行，指向它的链接由搬运器留空，产出的官方库里不会留下悬空引用。
    /// </summary>
    public sealed class OfficialRealmCopyFilter : IDynamicCopyFilter
    {
        private readonly DynamicRealmSession source;

        private readonly Dictionary<string, bool> rulesetIsOfficial = new(StringComparer.Ordinal);
        private readonly Dictionary<Guid, BeatmapFacts> beatmaps = new();
        private readonly Dictionary<Guid, bool> beatmapSetIsOfficial = new();
        private readonly HashSet<string> keptBeatmapHashes = new(StringComparer.Ordinal);
        private readonly HashSet<IRealmObjectBase> keptMetadata = new();

        private bool indexesBuilt;

        public OfficialRealmCopyFilter(DynamicRealmSession source)
        {
            this.source = source;
        }

        public bool ShouldCopy(string className, IRealmObjectBase sourceRow)
        {
            BuildIndexes();

            return className switch
            {
                OfficialBaselineSchema.Ruleset => rulesetIsOfficial.GetValueOrDefault(DynamicRealmAccess.GetString(sourceRow, "ShortName")),
                OfficialBaselineSchema.Beatmap => shouldCopyBeatmap(sourceRow),
                OfficialBaselineSchema.BeatmapSet => beatmapSetIsOfficial.GetValueOrDefault(DynamicRealmAccess.Get<Guid>(sourceRow, "ID")),
                OfficialBaselineSchema.Score => shouldCopyScore(sourceRow),
                // 元数据行跟着难度走：难度留下了才留，免得起草出一堆没人引用的元数据。
                OfficialBaselineSchema.BeatmapMetadata => keptMetadata.Contains(sourceRow),
                _ => true,
            };
        }

        /// <summary>只读源库一遍，把判定所需事实读齐。重复调用无副作用。</summary>
        public void BuildIndexes()
        {
            if (indexesBuilt)
                return;

            indexesBuilt = true;

            readRulesets();
            readBeatmaps();
            readBeatmapSets();
            finalizeBeatmaps();
        }

        private void readRulesets()
        {
            if (!source.HasClass(OfficialBaselineSchema.Ruleset))
                return;

            foreach (IRealmObjectBase ruleset in DynamicRealmAccess.All(source.Realm, OfficialBaselineSchema.Ruleset).AsEnumerable())
            {
                rulesetIsOfficial[DynamicRealmAccess.GetString(ruleset, "ShortName")] = OfficialExportPolicy.IsOfficialRuleset(
                    DynamicRealmAccess.GetString(ruleset, "ShortName"),
                    DynamicRealmAccess.GetString(ruleset, "InstantiationInfo"));
            }
        }

        private void readBeatmaps()
        {
            if (!source.HasClass(OfficialBaselineSchema.Beatmap))
                return;

            foreach (IRealmObjectBase beatmap in DynamicRealmAccess.All(source.Realm, OfficialBaselineSchema.Beatmap).AsEnumerable())
            {
                IRealmObjectBase? ruleset = DynamicRealmAccess.Get<IRealmObjectBase>(beatmap, "Ruleset");
                string rulesetShortName = DynamicRealmAccess.GetString(ruleset, "ShortName");
                IRealmObjectBase? parent = DynamicRealmAccess.Get<IRealmObjectBase>(beatmap, "BeatmapSet");

                beatmaps[DynamicRealmAccess.Get<Guid>(beatmap, "ID")] = new BeatmapFacts(
                    Hidden: DynamicRealmAccess.Get<bool>(beatmap, "Hidden"),
                    RulesetIsOfficial: rulesetIsOfficial.GetValueOrDefault(rulesetShortName),
                    SetId: parent == null ? Guid.Empty : DynamicRealmAccess.Get<Guid>(parent, "ID"),
                    Hash: DynamicRealmAccess.GetString(beatmap, "Hash"),
                    Metadata: DynamicRealmAccess.Get<IRealmObjectBase>(beatmap, "Metadata"));
            }
        }

        private void readBeatmapSets()
        {
            if (!source.HasClass(OfficialBaselineSchema.BeatmapSet))
                return;

            var factsPerSet = beatmaps.Values
                                      .Where(beatmap => beatmap.SetId != Guid.Empty)
                                      .GroupBy(beatmap => beatmap.SetId)
                                      .ToDictionary(group => group.Key, group => group.Select(b => b.Facts).ToList());

            foreach (IRealmObjectBase set in DynamicRealmAccess.All(source.Realm, OfficialBaselineSchema.BeatmapSet).AsEnumerable())
            {
                Guid id = DynamicRealmAccess.Get<Guid>(set, "ID");

                // 软删的行不搬：搬过去官方会当成「已删除」再清一遍，等于白搬。
                beatmapSetIsOfficial[id] = !DynamicRealmAccess.Get<bool>(set, "DeletePending")
                                           && OfficialExportPolicy.IsOfficialBeatmapSet(
                                               readHostingKind(set),
                                               factsPerSet.TryGetValue(id, out var facts) ? facts : []);
            }
        }

        /// <summary>难度是否留下还要看它所属的集：集被拒（外部托管 / 只含 Ez 规则集难度）时里面每一行都不搬。</summary>
        private void finalizeBeatmaps()
        {
            foreach ((_, BeatmapFacts facts) in beatmaps)
            {
                if (!isKept(facts))
                    continue;

                keptBeatmapHashes.Add(facts.Hash);

                if (facts.Metadata is { } metadata)
                    keptMetadata.Add(metadata);
            }
        }

        private bool shouldCopyBeatmap(IRealmObjectBase beatmap)
        {
            if (!beatmaps.TryGetValue(DynamicRealmAccess.Get<Guid>(beatmap, "ID"), out BeatmapFacts facts))
                return false;

            return isKept(facts);
        }

        private bool isKept(BeatmapFacts facts)
        {
            if (facts.SetId != Guid.Empty && !beatmapSetIsOfficial.GetValueOrDefault(facts.SetId))
                return false;

            return OfficialExportPolicy.IsOfficialBeatmap(facts.Hidden, facts.RulesetIsOfficial, parentSetIsOfficial: true);
        }

        private bool shouldCopyScore(IRealmObjectBase score)
        {
            if (DynamicRealmAccess.Get<bool>(score, "DeletePending"))
                return false;

            IRealmObjectBase? ruleset = DynamicRealmAccess.Get<IRealmObjectBase>(score, "Ruleset");

            if (!OfficialExportPolicy.IsOfficialScore(
                    DynamicRealmAccess.GetString(ruleset, "ShortName"),
                    readIntOrDefault(score, "ManiaHitMode"),
                    readIntOrDefault(score, "ManiaHealthMode"),
                    DynamicRealmAccess.GetString(score, "Mods")))
            {
                return false;
            }

            // 成绩靠 BeatmapHash 挂难度：难度没搬过去（含 hash 为空）的成绩搬过去就是官方端看不见的悬空行。
            return keptBeatmapHashes.Contains(DynamicRealmAccess.GetString(score, "BeatmapHash"));
        }

        private static int readIntOrDefault(IRealmObjectBase row, string property) =>
            DynamicRealmAccess.HasProperty(row, property) ? DynamicRealmAccess.Get<int>(row, property) : 0;

        /// <summary>HostingKind 是 Ez 扩展列（BeatmapSetHostingKind.External = 1）；官方库没有这列，按 0 处理。</summary>
        private static int readHostingKind(IRealmObjectBase set) => readIntOrDefault(set, "HostingKind");

        private readonly record struct BeatmapFacts(bool Hidden, bool RulesetIsOfficial, Guid SetId, string Hash, IRealmObjectBase? Metadata)
        {
            public BeatmapRowFacts Facts => new(Hidden, RulesetIsOfficial);
        }
    }
}
