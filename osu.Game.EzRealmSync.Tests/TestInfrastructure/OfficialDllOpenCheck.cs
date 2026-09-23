namespace osu.Game.EzRealmSync.Tests.TestInfrastructure
{
    /// <summary>
    /// 「产物能不能被官方读」的验收口径：交给 <see cref="OfficialDllVerifierProcess"/>——一个只加载
    /// <c>ppy.osu.Game</c> 的独立进程——以 pinned disk schema 打开并读一遍核心表。
    ///
    /// 走独立进程而不是进程内 pinned 打开：进程内只有 Ez 线的模型，用它打开官方产物会因为 Ez 修订号
    /// 不同而报 MigrationNeeded——那与产物对不对无关；而且官方 osu.Game.dll 与 Ez 版同名，无法共存。
    ///
    /// 注意 pinned 打开只是"够不够低"的门槛：版本相等时 realm-core 不做 schema 比对，因此它<b>不会</b>
    /// 发现文件里多出来的表/列。要验「收窄后逐列等于官方 schema」，用
    /// <see cref="OfficialDllVerifierProcess.DumpFileSchema"/> 与官方 schema 对拍。
    /// </summary>
    internal static class OfficialDllOpenCheck
    {
        /// <summary>
        /// 能否用官方 DLL 打开。<paramref name="error"/> 带回失败原因；验收器本身不可用时返回 false
        /// 并说明原因（调用方据此决定是断言还是跳过）。
        /// </summary>
        public static bool TryOpen(string realmPath, int pinnedSchemaVersion, out string? error)
        {
            error = null;

            if (!VerifierAvailable)
            {
                error = $"DllVerifier 未构建：{OfficialDllVerifierProcess.ResolveVerifierPathForTests()}";
                return false;
            }

            try
            {
                return OfficialDllVerifierProcess.TryOpen(realmPath, pinnedSchemaVersion, out error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>验收器是否就绪；未就绪时测试应 Assert.Ignore 而不是失败。</summary>
        public static bool VerifierAvailable => OfficialDllVerifierProcess.VerifierAvailable;
    }
}
