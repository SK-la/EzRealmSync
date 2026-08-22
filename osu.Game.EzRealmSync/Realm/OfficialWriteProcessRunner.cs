using System.Diagnostics;
using System.Text.Json;
using osu.Game.EzRealmSync.Contracts;

namespace osu.Game.EzRealmSync.Realm
{
    public static class OfficialWriteProcessRunner
    {
        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static OfficialConvertResult Run(OfficialConvertJob job, CancellationToken cancellationToken = default)
        {
            string workerPath = resolveWorkerExecutablePath();
            if (!File.Exists(workerPath))
                throw new InvalidOperationException($"未找到转官方写库 Worker：{workerPath}");

            string tempRoot = EzRealmSyncDataPaths.CreateTempSubdirectory("official-write-job");
            string jobPath = Path.Combine(tempRoot, "job.json");
            string resultPath = Path.Combine(tempRoot, "result.json");

            try
            {
                File.WriteAllText(jobPath, JsonSerializer.Serialize(job, jsonOptions));

                var psi = CreateWorkerStartInfo(workerPath, new[] { jobPath, resultPath });
                EzRealmSyncLog.Info($"Starting OfficialWrite worker: {workerPath}");

                var runResult = WorkerProcessExecution.Run(psi, cancellationToken);

                if (!File.Exists(resultPath))
                {
                    string message = WorkerProcessExecution.BuildFailureMessage("OfficialWrite Worker", workerPath, runResult, "未产出 result.json");
                    EzRealmSyncLog.Error(message);
                    throw new InvalidOperationException(message);
                }

                var result = JsonSerializer.Deserialize<OfficialConvertResult>(File.ReadAllText(resultPath), jsonOptions)
                             ?? throw new InvalidOperationException("OfficialWrite Worker 结果 JSON 无效。");

                if (!result.Success)
                {
                    string message = result.ErrorMessage ?? "OfficialWrite Worker 失败。";
                    EzRealmSyncLog.Error($"OfficialWrite Worker reported failure (exit {runResult.ExitCode}): {message}");
                    throw new InvalidOperationException(message);
                }

                return result;
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    try
                    {
                        Directory.Delete(tempRoot, recursive: true);
                    }
                    catch
                    {
                        // 临时 job 目录清理失败不影响主流程。
                    }
                }
            }
        }

        public static string ResolveWorkerExecutablePathForTests() => resolveWorkerExecutablePath();

        public static ProcessStartInfo CreateWorkerStartInfo(string workerPath, IReadOnlyList<string> arguments) =>
            WorkerProcessExecution.CreateWorkerStartInfo(workerPath, arguments);

        private static string resolveWorkerExecutablePath()
        {
            string baseDir = AppContext.BaseDirectory;

            foreach (string candidate in new[]
                     {
                         Path.Combine(baseDir, "official-write", "EzRealmSync.OfficialWrite.exe"),
                         Path.Combine(baseDir, "official-write", "EzRealmSync.OfficialWrite.dll"),
                         Path.Combine(baseDir, "EzRealmSync.OfficialWrite.exe"),
                         Path.Combine(baseDir, "EzRealmSync.OfficialWrite.dll"),
                         Path.Combine(baseDir, "..", "osu.Game.EzRealmSync.OfficialWrite", "bin", "Debug", "net8.0", "EzRealmSync.OfficialWrite.exe"),
                         Path.Combine(baseDir, "..", "osu.Game.EzRealmSync.OfficialWrite", "bin", "Debug", "net8.0", "EzRealmSync.OfficialWrite.dll"),
                         Path.Combine(baseDir, "..", "..", "osu.Game.EzRealmSync.OfficialWrite", "bin", "Debug", "net8.0", "EzRealmSync.OfficialWrite.exe"),
                         Path.Combine(baseDir, "..", "..", "osu.Game.EzRealmSync.OfficialWrite", "bin", "Debug", "net8.0", "EzRealmSync.OfficialWrite.dll"),
                     })
            {
                string full = Path.GetFullPath(candidate);
                if (File.Exists(full))
                    return full;
            }

            return Path.Combine(baseDir, "official-write", "EzRealmSync.OfficialWrite.dll");
        }
    }
}
