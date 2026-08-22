using System.Diagnostics;
using System.Text;

namespace osu.Game.EzRealmSync.Realm
{
    public readonly struct WorkerProcessResult
    {
        public int ExitCode { get; init; }

        public string StandardOutput { get; init; }

        public string StandardError { get; init; }
    }

    public static class WorkerProcessExecution
    {
        public static ProcessStartInfo CreateWorkerStartInfo(string workerPath, IReadOnlyList<string> arguments)
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

        public static WorkerProcessResult Run(ProcessStartInfo psi, CancellationToken cancellationToken = default)
        {
            using var process = Process.Start(psi)
                                  ?? throw new InvalidOperationException("无法启动 Worker 进程。");

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

        public static string BuildFailureMessage(string workerLabel, string workerPath, WorkerProcessResult result, string? extra = null)
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
    }
}
