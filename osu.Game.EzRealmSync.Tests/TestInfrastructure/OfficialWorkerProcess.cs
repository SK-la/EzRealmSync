using System.Diagnostics;
using System.Text;
using System.Text.Json;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.IO;
using osu.Game.EzRealmSync.Realm;

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

                var psi = CreateWorkerStartInfo(ResolveWorkerExecutablePathForTests(), new[] { jobPath, resultPath });
                var runResult = RunProcess(psi, cancellationToken);

                if (!File.Exists(resultPath))
                    throw new InvalidOperationException(BuildFailureMessage("OfficialWrite Worker", psi.FileName, runResult, "未产出 result.json"));

                var result = JsonSerializer.Deserialize<OfficialConvertResult>(File.ReadAllText(resultPath), json_options)
                             ?? throw new InvalidOperationException("OfficialWrite Worker 结果 JSON 无效。");

                if (!result.Success)
                    throw new InvalidOperationException(result.ErrorMessage ?? "OfficialWrite Worker 失败。");

                return result;
            }
            finally
            {
                deleteTempRoot(tempRoot);
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

                var psi = CreateWorkerStartInfo(workerPath, new[] { mode, jobPath, resultPath });
                var runResult = RunProcess(psi, cancellationToken);

                if (!File.Exists(resultPath))
                    throw new InvalidOperationException(BuildFailureMessage("Official Worker", workerPath, runResult, "未产出 result.json"));

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
                deleteTempRoot(tempRoot);
            }
        }

        private static ProcessStartInfo CreateWorkerStartInfo(string workerPath, IReadOnlyList<string> arguments)
        {
            string fullWorkerPath = Path.GetFullPath(workerPath);
            string workerDir = Path.GetDirectoryName(fullWorkerPath) ?? AppContext.BaseDirectory;

            var psi = new ProcessStartInfo
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = workerDir,
            };

            if (fullWorkerPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                psi.FileName = "dotnet";
                psi.ArgumentList.Add("exec");
                psi.ArgumentList.Add(fullWorkerPath);
            }
            else
            {
                psi.FileName = fullWorkerPath;
            }

            foreach (string arg in arguments)
                psi.ArgumentList.Add(arg);

            return psi;
        }

        private static WorkerProcessResult RunProcess(ProcessStartInfo psi, CancellationToken cancellationToken)
        {
            using var process = Process.Start(psi) ?? throw new InvalidOperationException("无法启动 Worker 进程。");

            using (cancellationToken.Register(state =>
            {
                var p = (Process?)state;

                try
                {
                    if (p != null && !p.HasExited)
                        p.Kill(entireProcessTree: true);
                }
                catch
                {
                    // 取消路径忽略 kill 失败。
                }
            }, process))
            {
                var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
                process.WaitForExit();

                return new WorkerProcessResult
                {
                    ExitCode = process.ExitCode,
                    StandardOutput = stdoutTask.GetAwaiter().GetResult(),
                    StandardError = stderrTask.GetAwaiter().GetResult(),
                };
            }
        }

        private static string BuildFailureMessage(string workerLabel, string workerPath, WorkerProcessResult result, string? extra = null)
        {
            var sb = new StringBuilder();
            sb.Append($"{workerLabel} 失败（exit {result.ExitCode}，worker={workerPath}）");

            if (!string.IsNullOrWhiteSpace(extra))
                sb.AppendLine().Append(extra);

            if (!string.IsNullOrWhiteSpace(result.StandardError))
                sb.AppendLine().Append("stderr: ").Append(result.StandardError.Trim());

            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
                sb.AppendLine().Append("stdout: ").Append(result.StandardOutput.Trim());

            return sb.ToString().Trim();
        }

        private static void deleteTempRoot(string tempRoot)
        {
            if (!Directory.Exists(tempRoot))
                return;

            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // 临时 job 目录清理失败不影响主流程。
            }
        }

        private readonly struct WorkerProcessResult
        {
            public int ExitCode { get; init; }

            public string StandardOutput { get; init; }

            public string StandardError { get; init; }
        }
    }
}
