using System.Diagnostics;
using System.Text.Json;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Realm.Readers;

namespace osu.Game.EzRealmSync.Realm
{
    public static class RealmReadSidecarRunner
    {
        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static RealmReadResult ReadDiffSnapshot(RealmReaderPackageInfo package, RealmReadJob job, CancellationToken cancellationToken = default) =>
            run<RealmReadResult>(package, "read", job, cancellationToken);

        public static RealmApplyExportResult ExportApplyBundle(RealmReaderPackageInfo package, RealmApplyExportJob job, CancellationToken cancellationToken = default) =>
            run<RealmApplyExportResult>(package, "apply-export", job, cancellationToken);

        public static string ResolveWorkerExecutablePathForTests() => resolveWorkerExecutablePath();

        private static T run<T>(RealmReaderPackageInfo package, string mode, object job, CancellationToken cancellationToken)
            where T : new()
        {
            string workerPath = resolveWorkerExecutablePath();
            if (!File.Exists(workerPath))
                throw new InvalidOperationException($"未找到 ReadSidecar Worker：{workerPath}");

            string tempRoot = EzRealmSyncDataPaths.CreateTempSubdirectory("read-sidecar-job");
            string jobPath = Path.Combine(tempRoot, "job.json");
            string resultPath = Path.Combine(tempRoot, "result.json");

            try
            {
                File.WriteAllText(jobPath, JsonSerializer.Serialize(job, jsonOptions));

                var psi = OfficialWriteProcessRunner.CreateWorkerStartInfo(workerPath, new[] { mode, jobPath, resultPath });

                using var process = Process.Start(psi)
                    ?? throw new InvalidOperationException("无法启动 ReadSidecar Worker。");

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
                        $"ReadSidecar Worker 未产出结果（exit {process.ExitCode}）：{stderr}".Trim());
                }

                var result = JsonSerializer.Deserialize<T>(File.ReadAllText(resultPath), jsonOptions)
                             ?? throw new InvalidOperationException("ReadSidecar Worker 结果 JSON 无效。");

                if (result is RealmReadResult readResult && !readResult.Success)
                    throw new InvalidOperationException(readResult.ErrorMessage ?? "ReadSidecar 读取失败。");

                if (result is RealmApplyExportResult exportResult && !exportResult.Success)
                    throw new InvalidOperationException(exportResult.ErrorMessage ?? "ReadSidecar 导出失败。");

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

        private static string resolveWorkerExecutablePath()
        {
            string baseDir = AppContext.BaseDirectory;

            foreach (string candidate in new[]
                     {
                         Path.Combine(baseDir, "read-sidecar", "EzRealmSync.ReadSidecar.dll"),
                         Path.Combine(baseDir, "read-sidecar", "EzRealmSync.ReadSidecar.exe"),
                         Path.Combine(baseDir, "EzRealmSync.ReadSidecar.dll"),
                         Path.Combine(baseDir, "EzRealmSync.ReadSidecar.exe"),
                         Path.Combine(baseDir, "..", "osu.Game.EzRealmSync.ReadSidecar", "bin", "Debug", "net8.0", "EzRealmSync.ReadSidecar.dll"),
                         Path.Combine(baseDir, "..", "..", "osu.Game.EzRealmSync.ReadSidecar", "bin", "Debug", "net8.0", "EzRealmSync.ReadSidecar.dll"),
                     })
            {
                string full = Path.GetFullPath(candidate);
                if (File.Exists(full))
                    return full;
            }

            return Path.Combine(baseDir, "read-sidecar", "EzRealmSync.ReadSidecar.dll");
        }
    }
}
