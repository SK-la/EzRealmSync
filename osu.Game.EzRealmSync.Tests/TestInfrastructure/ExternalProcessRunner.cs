using System.Diagnostics;
using System.Text;

namespace osu.Game.EzRealmSync.Tests.TestInfrastructure
{
    /// <summary>跑外部进程并带回 stdout/stderr，供 worker 类验收夹具共用。</summary>
    internal static class ExternalProcessRunner
    {
        public static ProcessRunResult Run(string executablePath, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            string fullPath = Path.GetFullPath(executablePath);

            var psi = new ProcessStartInfo
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(fullPath) ?? AppContext.BaseDirectory,
            };

            if (fullPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                psi.FileName = "dotnet";
                psi.ArgumentList.Add("exec");
                psi.ArgumentList.Add(fullPath);
            }
            else
            {
                psi.FileName = fullPath;
            }

            foreach (string argument in arguments)
                psi.ArgumentList.Add(argument);

            using var process = Process.Start(psi) ?? throw new InvalidOperationException($"无法启动进程：{fullPath}");

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

                return new ProcessRunResult(process.ExitCode, stdoutTask.GetAwaiter().GetResult(), stderrTask.GetAwaiter().GetResult());
            }
        }

        public static string DescribeFailure(string label, string executablePath, ProcessRunResult result, string? extra = null)
        {
            var sb = new StringBuilder();
            sb.Append($"{label} 失败（exit {result.ExitCode}，worker={executablePath}）");

            if (!string.IsNullOrWhiteSpace(extra))
                sb.AppendLine().Append(extra);

            if (!string.IsNullOrWhiteSpace(result.StandardError))
                sb.AppendLine().Append("stderr: ").Append(result.StandardError.Trim());

            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
                sb.AppendLine().Append("stdout: ").Append(result.StandardOutput.Trim());

            return sb.ToString().Trim();
        }

        public static void DeleteDirectoryQuietly(string path)
        {
            if (!Directory.Exists(path))
                return;

            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch
            {
                // 临时目录清理失败不影响主流程。
            }
        }
    }

    internal readonly record struct ProcessRunResult(int ExitCode, string StandardOutput, string StandardError);
}
