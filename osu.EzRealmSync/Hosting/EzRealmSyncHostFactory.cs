namespace osu.EzRealmSync.Hosting
{
    public enum EzRealmSyncHostKind
    {
        /// <summary>独立进程（当前默认、已实现）。</summary>
        Standalone,

        /// <summary>在 osu 客户端内挂载（未实现，预留）。</summary>
        RulesetEmbedded,
    }

    public static class EzRealmSyncHostFactory
    {
        public static IEzRealmSyncHost Create(EzRealmSyncHostKind kind = EzRealmSyncHostKind.Standalone) => kind switch
        {
            EzRealmSyncHostKind.Standalone => new StandaloneEzRealmSyncHost(),
            EzRealmSyncHostKind.RulesetEmbedded => throw new NotSupportedException(
                "规则集内嵌宿主尚未实现。请使用独立 EzRealmSync.exe，或 --ui-test 调试 UI。"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }
}
