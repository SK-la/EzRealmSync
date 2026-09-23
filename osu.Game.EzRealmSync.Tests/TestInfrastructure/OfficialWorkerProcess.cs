using System.Text.Json;
using osu.Game.EzRealmSync.Contracts;

namespace osu.Game.EzRealmSync.Tests.TestInfrastructure
{
    /// <summary>
    /// 测试夹具：以独立进程跑 <c>EzRealmSync.OfficialWrite</c> Worker（内部加载 osu.Game.dll），
    /// 用来验证「本工具产出的库能否被官方模型打开」。产品工程不得引用本文件。
    /// </summary>
    public static class OfficialWorkerProcess
    {
        private static readonly JsonSerializerOptions json_options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static OfficialConvertResult Run(OfficialConvertJob job, CancellationToken cancellationToken = default)
        {
            string tempRoot = EzRealmSyncDataPaths.CreateTempSubdirectory("official-write-job");
            string jobPath = Path.Combine(tempRoot, "job.json");
            string resultPath = Path.Combine(tempRoot, "result.json");

            try
            {
                File.WriteAllText(jobPath, JsonSerializer.Serialize(job, json_options));

                string[] psi = new[] { jobPath, resultPath };
                var runResult = ExternalProcessRunner.Run(ResolveWorkerExecutablePathForTests(), psi, cancellationToken);

                if (!File.Exists(resultPath))
                    throw new InvalidOperationException(ExternalProcessRunner.DescribeFailure("OfficialWrite Worker", ResolveWorkerExecutablePathForTests(), runResult, "未产出 result.json"));

                var result = JsonSerializer.Deserialize<OfficialConvertResult>(File.ReadAllText(resultPath), json_options)
                             ?? throw new InvalidOperationException("OfficialWrite Worker 结果 JSON 无效。");

                if (!result.Success)
                    throw new InvalidOperationException(result.ErrorMessage ?? "OfficialWrite Worker 失败。");

                return result;
            }
            finally
            {
                ExternalProcessRunner.DeleteDirectoryQuietly(tempRoot);
            }
        }

        public static RealmReadResult Read(RealmReadJob job, CancellationToken cancellationToken = default) =>
            runRead<RealmReadResult>("read", job, cancellationToken);

        public static RealmBrowseResult Browse(RealmBrowseJob job, CancellationToken cancellationToken = default) =>
            runRead<RealmBrowseResult>("browse", job, cancellationToken);

        public static string ResolveWorkerExecutablePathForTests()
        {
            string baseDir = AppContext.BaseDirectory;

            foreach (string candidate in new[]
                     {
                         Path.Combine(baseDir, "official-write", "EzRealmSync.OfficialWrite.exe"),
                         Path.Combine(baseDir, "official-write", "EzRealmSync.OfficialWrite.dll"),
                         Path.Combine(baseDir, "EzRealmSync.OfficialWrite.exe"),
                         Path.Combine(baseDir, "EzRealmSync.OfficialWrite.dll"),
                         Path.Combine(baseDir, "..", "osu.Game.EzRealmSync.OfficialWrite", "bin", "Debug", "net10.0", "EzRealmSync.OfficialWrite.exe"),
                         Path.Combine(baseDir, "..", "osu.Game.EzRealmSync.OfficialWrite", "bin", "Debug", "net10.0", "EzRealmSync.OfficialWrite.dll"),
                         Path.Combine(baseDir, "..", "..", "osu.Game.EzRealmSync.OfficialWrite", "bin", "Debug", "net10.0", "EzRealmSync.OfficialWrite.exe"),
                         Path.Combine(baseDir, "..", "..", "osu.Game.EzRealmSync.OfficialWrite", "bin", "Debug", "net10.0", "EzRealmSync.OfficialWrite.dll"),
                     })
            {
                string full = Path.GetFullPath(candidate);
                if (File.Exists(full))
                    return full;
            }

            return Path.Combine(baseDir, "official-write", "EzRealmSync.OfficialWrite.dll");
        }

        private static TResult runRead<TResult>(string mode, object job, CancellationToken cancellationToken)
            where TResult : class
        {
            string workerPath = ResolveWorkerExecutablePathForTests();
            if (!File.Exists(workerPath))
                throw new InvalidOperationException($"未找到 Official Worker：{workerPath}");

            string tempRoot = EzRealmSyncDataPaths.CreateTempSubdirectory("official-read-job");
            string jobPath = Path.Combine(tempRoot, "job.json");
            string resultPath = Path.Combine(tempRoot, "result.json");

            try
            {
                File.WriteAllText(jobPath, JsonSerializer.Serialize(job, json_options));

                string[] psi = new[] { mode, jobPath, resultPath };
                var runResult = ExternalProcessRunner.Run(workerPath, psi, cancellationToken);

                if (!File.Exists(resultPath))
                    throw new InvalidOperationException(ExternalProcessRunner.DescribeFailure("Official Worker", workerPath, runResult, "未产出 result.json"));

                var result = JsonSerializer.Deserialize<TResult>(File.ReadAllText(resultPath), json_options)
                             ?? throw new InvalidOperationException("Official Worker 结果 JSON 无效。");

                string? error = result switch
                {
                    RealmBrowseResult browse when !browse.Success => browse.ErrorMessage ?? "Official Worker browse 失败。",
                    RealmReadResult read when !read.Success => read.ErrorMessage ?? "Official Worker read 失败。",
                    _ => null,
                };

                if (error != null)
                    throw new InvalidOperationException(error);

                return result;
            }
            finally
            {
                ExternalProcessRunner.DeleteDirectoryQuietly(tempRoot);
            }
        }
    }
}
