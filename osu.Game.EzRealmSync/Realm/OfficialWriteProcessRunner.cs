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

                var psi = createWorkerStartInfo(workerPath, new[] { jobPath, resultPath });

                using var process = Process.Start(psi)
                    ?? throw new InvalidOperationException("无法启动 OfficialWrite Worker。");

                using (cancellationToken.Register(() =>
                       {
                           try
                           {
                               if (!process.HasExited)
                                   process.Kill(entireProcessTree: true);
                           }
                           catch
                           {
                               // 取消路径忽略 kill 失败。
                           }
                       }))
                {
                    process.WaitForExit();
                }

                if (!File.Exists(resultPath))
                {
                    string stderr = process.StandardError.ReadToEnd();
                    throw new InvalidOperationException(
                        $"OfficialWrite Worker 未产出结果（exit {process.ExitCode}）：{stderr}".Trim());
                }

                var result = JsonSerializer.Deserialize<OfficialConvertResult>(File.ReadAllText(resultPath), jsonOptions)
                             ?? throw new InvalidOperationException("OfficialWrite Worker 结果 JSON 无效。");

                if (!result.Success)
                    throw new InvalidOperationException(result.ErrorMessage ?? "OfficialWrite Worker 失败。");

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
            createWorkerStartInfo(workerPath, arguments);

        private static ProcessStartInfo createWorkerStartInfo(string workerPath, IReadOnlyList<string> arguments)
        {
            string workerDir = Path.GetDirectoryName(workerPath) ?? AppContext.BaseDirectory;
            string dllPath = Path.Combine(workerDir, "EzRealmSync.OfficialWrite.dll");

            var psi = new ProcessStartInfo
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = workerDir,
            };

            if (File.Exists(dllPath))
            {
                psi.FileName = "dotnet";
                psi.ArgumentList.Add("exec");
                psi.ArgumentList.Add(dllPath);
            }
            else
            {
                psi.FileName = workerPath;
            }

            foreach (string arg in arguments)
                psi.ArgumentList.Add(arg);

            return psi;
        }

        private static string resolveWorkerExecutablePath()
        {
            string baseDir = AppContext.BaseDirectory;

            foreach (string candidate in new[]
                     {
                         Path.Combine(baseDir, "official-write", "EzRealmSync.OfficialWrite.dll"),
                         Path.Combine(baseDir, "official-write", "EzRealmSync.OfficialWrite.exe"),
                         Path.Combine(baseDir, "EzRealmSync.OfficialWrite.dll"),
                         Path.Combine(baseDir, "EzRealmSync.OfficialWrite.exe"),
                         Path.Combine(baseDir, "..", "osu.Game.EzRealmSync.OfficialWrite", "bin", "Debug", "net8.0", "EzRealmSync.OfficialWrite.dll"),
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
