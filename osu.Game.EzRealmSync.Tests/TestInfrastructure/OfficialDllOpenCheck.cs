using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Realm;

namespace osu.Game.EzRealmSync.Tests.TestInfrastructure
{
    /// <summary>
    /// 「产物能不能被官方读」的验收口径：交给 official-write Worker（独立的官方 schema 镜像进程）
    /// 以 pinned disk schema 打开并读一遍。
    ///
    /// 走独立进程而不是进程内 pinned 打开：进程内只有 Ez 线的模型，用它打开官方产物会因为
    /// Ez 修订号不同而报 MigrationNeeded——那与产物对不对无关。
    /// </summary>
    internal static class OfficialDllOpenCheck
    {
        /// <summary>
        /// 能否用官方 DLL 打开。<paramref name="error"/> 带回失败原因；Worker 本身不可用时返回 false
        /// 并说明原因（调用方据此决定是断言还是跳过）。
        /// </summary>
        public static bool TryOpen(string realmPath, int pinnedSchemaVersion, out string? error)
        {
            error = null;

            string worker;

            try
            {
                worker = OfficialWriteProcessRunner.ResolveWorkerExecutablePathForTests();
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            if (!File.Exists(worker))
            {
                error = $"Official Worker 未复制到测试输出：{worker}";
                return false;
            }

            try
            {
                var result = OfficialReadProcessRunner.Read(new RealmReadJob
                {
                    ReaderLibDirectory = string.Empty,
                    RealmFilePath = realmPath,
                    PinnedDiskSchemaVersion = pinnedSchemaVersion,
                    Profile = "official",
                });

                if (result.Success)
                    return true;

                error = result.ErrorMessage ?? "官方 DLL 读失败，未给出原因。";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>Worker 是否就绪；未就绪时测试应 Assert.Ignore 而不是失败。</summary>
        public static bool WorkerAvailable =>
            File.Exists(OfficialWriteProcessRunner.ResolveWorkerExecutablePathForTests());
    }
}
