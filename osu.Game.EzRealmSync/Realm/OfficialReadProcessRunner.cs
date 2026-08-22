using System.Text.Json;
using osu.Game.EzRealmSync.Contracts;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 官方磁盘 schema 只读：browse / read / apply-export，复用 official-write Worker（OfficialSchema 镜像）。
    /// </summary>
    public static class OfficialReadProcessRunner
    {
        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static RealmBrowseResult Browse(RealmBrowseJob job, CancellationToken cancellationToken = default) =>
            run<RealmBrowseResult>("browse", job, cancellationToken);

        public static RealmReadResult Read(RealmReadJob job, CancellationToken cancellationToken = default) =>
            run<RealmReadResult>("read", job, cancellationToken);

        public static RealmApplyExportResult ExportApplyBundle(RealmApplyExportJob job, CancellationToken cancellationToken = default) =>
            run<RealmApplyExportResult>("apply-export", job, cancellationToken);

        public static OfficialApplyImportResult ApplyImport(OfficialApplyImportJob job, CancellationToken cancellationToken = default) =>
            run<OfficialApplyImportResult>("apply-import", job, cancellationToken);

        private static TResult run<TResult>(string mode, object job, CancellationToken cancellationToken)
            where TResult : class
        {
            string workerPath = OfficialWriteProcessRunner.ResolveWorkerExecutablePathForTests();
            if (!File.Exists(workerPath))
                throw new InvalidOperationException($"未找到 Official Worker：{workerPath}");

            string tempRoot = EzRealmSyncDataPaths.CreateTempSubdirectory("official-read-job");
            string jobPath = Path.Combine(tempRoot, "job.json");
            string resultPath = Path.Combine(tempRoot, "result.json");

            try
            {
                File.WriteAllText(jobPath, JsonSerializer.Serialize(job, jsonOptions));

                var psi = OfficialWriteProcessRunner.CreateWorkerStartInfo(workerPath, new[] { mode, jobPath, resultPath });
                EzRealmSyncLog.Info($"Starting Official Worker ({mode}): {workerPath}");

                var runResult = WorkerProcessExecution.Run(psi, cancellationToken);

                if (!File.Exists(resultPath))
                {
                    string message = WorkerProcessExecution.BuildFailureMessage("Official Worker", workerPath, runResult, "未产出 result.json");
                    EzRealmSyncLog.Error(message);
                    throw new InvalidOperationException(message);
                }

                var result = JsonSerializer.Deserialize<TResult>(File.ReadAllText(resultPath), jsonOptions)
                             ?? throw new InvalidOperationException("Official Worker 结果 JSON 无效。");

                if (result is RealmBrowseResult browse && !browse.Success)
                {
                    string message = browse.ErrorMessage ?? "Official Worker browse 失败。";
                    EzRealmSyncLog.Error($"Official Worker browse failed (exit {runResult.ExitCode}): {message}");
                    throw new InvalidOperationException(message);
                }

                if (result is RealmReadResult read && !read.Success)
                {
                    string message = read.ErrorMessage ?? "Official Worker read 失败。";
                    EzRealmSyncLog.Error($"Official Worker read failed (exit {runResult.ExitCode}): {message}");
                    throw new InvalidOperationException(message);
                }

                if (result is RealmApplyExportResult export && !export.Success)
                {
                    string message = export.ErrorMessage ?? "Official Worker apply-export 失败。";
                    EzRealmSyncLog.Error($"Official Worker apply-export failed (exit {runResult.ExitCode}): {message}");
                    throw new InvalidOperationException(message);
                }

                if (result is OfficialApplyImportResult import && !import.Success)
                {
                    string message = import.ErrorMessage ?? "Official Worker apply-import 失败。";
                    EzRealmSyncLog.Error($"Official Worker apply-import failed (exit {runResult.ExitCode}): {message}");
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
    }
}
