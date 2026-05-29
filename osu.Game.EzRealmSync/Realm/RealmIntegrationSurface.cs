#if HAS_EZ_OSU_GAME
// 本文件列出 Phase 2 预期从 lib/osu.Game.dll 引用的主要类型（实现 Diff/写入时对照）。
// 不在此文件写业务逻辑，避免与 P2.2–P2.4 实现冲突。

using osu.Game.Database;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 文档化数据层对 osu.Game 的依赖面；编译通过即表示 lib 版本包含下列 API。
    /// </summary>
    internal static class RealmIntegrationSurface
    {
        // Ez 端：完整 schema（含 EZ_REALM_SCHEMA_VERSION）
        private static readonly System.Type ez_access = typeof(RealmAccess);

        // 官方端：仅上游 schema 51（实现后替换为 OfficialRealmAccess）
        // private static readonly System.Type official_access = typeof(OfficialRealmAccess);
    }
}

#endif
