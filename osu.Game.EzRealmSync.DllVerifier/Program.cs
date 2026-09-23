using System.Text.Json;

namespace osu.Game.EzRealmSync.DllVerifier
{
    /// <summary>
    /// 「工具处理完的库能不能被官方客户端打开」的进程外验收器。
    ///
    /// 本工程是唯一引用真实官方包（<c>ppy.osu.Game</c>）的地方。它用的是官方包里 Fody 织入的
    /// 模型与 schema，不是 Ez 侧手抄的镜像，所以它的判定等同于「官方客户端自己能不能开这份文件」。
    ///
    /// 两种调用：
    /// <list type="bullet">
    /// <item><c>verify-open &lt;job.json&gt; [result.json]</c>：按钉死的版本打开、读一遍核心表、可选清理软删。</item>
    /// <item><c>dump-schema [result.json]</c>：导出官方模型的完整 schema（类 → 列 → 类型/可空/主键/索引）。</item>
    /// </list>
    ///
    /// 输入输出都是纯 JSON，刻意不共享 Ez 侧 DTO：这个进程里不能出现任何 Ez 程序集。
    /// </summary>
    internal static class Program
    {
        private static readonly JsonSerializerOptions json_options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };

        public static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0 || isHelp(args[0]))
                {
                    printUsage();
                    return 2;
                }

                if (string.Equals(args[0], "dump-schema", StringComparison.OrdinalIgnoreCase))
                    return runDumpSchema(args);

                if (string.Equals(args[0], "dump-file-schema", StringComparison.OrdinalIgnoreCase))
                    return runDumpFileSchema(args);

                if (string.Equals(args[0], "verify-open", StringComparison.OrdinalIgnoreCase))
                    return runVerifyOpen(args);

                Console.Error.WriteLine($"未知子命令：{args[0]}");
                printUsage();
                return 2;
            }
            catch (Exception ex)
            {
                // 兜底：验收器自己崩了也要给出非零退出码与原因，不能让调用方把它当"打开成功"。
                Console.Error.WriteLine($"DllVerifier 未捕获异常：{ex}");
                return 3;
            }
        }

        private static bool isHelp(string arg) =>
            arg is "-h" or "--help" or "/?" or "help";

        private static void printUsage()
        {
            Console.Error.WriteLine("Usage: EzRealmSync.DllVerifier verify-open <job.json> [result.json]");
            Console.Error.WriteLine("       EzRealmSync.DllVerifier dump-schema [result.json]");
            Console.Error.WriteLine("       EzRealmSync.DllVerifier dump-file-schema <realmPath> [result.json]");
        }

        private static int runVerifyOpen(string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
            {
                printUsage();
                return 2;
            }

            string jobPath = Path.GetFullPath(args[1]);
            string resultPath = resolveResultPath(args, 2, jobPath);

            string jobJson = File.ReadAllText(jobPath);

            return writeResult(resultPath, () => OfficialOpenProbe.Run(jobJson));
        }

        private static int runDumpSchema(string[] args)
        {
            string resultPath = resolveResultPath(args, 1, Path.Combine(Path.GetTempPath(), "ezrealm-schema.json"));

            return writeResult(resultPath, OfficialSchemaDump.Run);
        }

        private static int runDumpFileSchema(string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
            {
                printUsage();
                return 2;
            }

            string realmPath = args[1];
            string resultPath = resolveResultPath(args, 2, Path.GetFullPath(realmPath) + ".schema.json");

            return writeResult(resultPath, () => OfficialSchemaDump.RunFileSchema(realmPath));
        }

        private static string resolveResultPath(string[] args, int index, string fallback)
            => args.Length > index && !string.IsNullOrWhiteSpace(args[index])
                ? Path.GetFullPath(args[index])
                : fallback + ".result.json";

        /// <summary>执行一次验收并把结果写成 JSON；异常也落成 <c>success:false</c>，退出码区分「打开失败」与「验收器故障」。</summary>
        private static int writeResult(string resultPath, Func<VerificationOutcome> action)
        {
            VerificationOutcome outcome;

            try
            {
                outcome = action();
            }
            catch (Exception ex)
            {
                outcome = VerificationOutcome.Failure(ExceptionFormatting.Describe(ex));
                write(resultPath, outcome);
                return 1;
            }

            write(resultPath, outcome);
            return outcome.Success ? 0 : 1;
        }

        private static void write(string resultPath, VerificationOutcome outcome)
        {
            string json = JsonSerializer.Serialize(outcome, json_options);

            string? directory = Path.GetDirectoryName(resultPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(resultPath, json);

            // 顺手打到 stdout：进程外调用失败时，调用方即使拿不到 result.json 也有线索。
            Console.WriteLine(json);
        }
    }
}
