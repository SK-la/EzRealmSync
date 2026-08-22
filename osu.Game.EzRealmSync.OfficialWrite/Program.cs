using System.Text.Json;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.OfficialSchema;

namespace osu.Game.EzRealmSync.OfficialWrite;

internal static class Program
{
    private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static int Main(string[] args)
    {
        if (args.Length >= 1 && string.Equals(args[0], "--verify", StringComparison.OrdinalIgnoreCase))
            return runVerify(args);

        if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.Error.WriteLine("Usage: EzRealmSync.OfficialWrite <job.json> [result.json]");
            Console.Error.WriteLine("       EzRealmSync.OfficialWrite --verify <verify-job.json> [result.json]");
            return 2;
        }

        return runWrite(args);
    }

    private static int runWrite(string[] args)
    {
        string jobPath = Path.GetFullPath(args[0]);
        string resultPath = args.Length >= 2 && !string.IsNullOrWhiteSpace(args[1])
            ? Path.GetFullPath(args[1])
            : jobPath + ".result.json";

        try
        {
            var job = JsonSerializer.Deserialize<OfficialConvertJob>(File.ReadAllText(jobPath), jsonOptions)
                      ?? throw new InvalidOperationException("job.json 为空或格式无效。");

            OfficialConvertResult result = OfficialMirrorRealmWriter.Write(job);
            File.WriteAllText(resultPath, JsonSerializer.Serialize(result, jsonOptions));
            return result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            writeFailure(resultPath, ExceptionFormatting.SafeFormat(ex));
            return 1;
        }
    }

    private static int runVerify(string[] args)
    {
        if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            Console.Error.WriteLine("--verify 需要 verify-job.json");
            return 2;
        }

        string verifyJobPath = Path.GetFullPath(args[1]);
        string resultPath = args.Length >= 3 && !string.IsNullOrWhiteSpace(args[2])
            ? Path.GetFullPath(args[2])
            : verifyJobPath + ".result.json";

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(verifyJobPath));
            var jobElement = doc.RootElement.GetProperty("job");
            var job = jobElement.Deserialize<OfficialConvertJob>(jsonOptions)
                      ?? throw new InvalidOperationException("verify job 无效。");

            int sourceFileHashCount = doc.RootElement.TryGetProperty("sourceFileHashCount", out var countElement)
                ? countElement.GetInt32()
                : 0;

            var (success, error, fileCount) = OfficialMirrorVerifier.Verify(
                job.TargetRealmPath,
                job.TargetUpstreamSchema,
                sourceFileHashCount);

            var payload = new
            {
                success,
                errorMessage = error,
                realmFileCount = fileCount,
            };

            File.WriteAllText(resultPath, JsonSerializer.Serialize(payload, jsonOptions));
            return success ? 0 : 1;
        }
        catch (Exception ex)
        {
            writeFailure(resultPath, ExceptionFormatting.SafeFormat(ex));
            return 1;
        }
    }

    private static void writeFailure(string resultPath, string message)
    {
        try
        {
            var failure = new OfficialConvertResult
            {
                Success = false,
                ErrorMessage = message,
            };
            File.WriteAllText(resultPath, JsonSerializer.Serialize(failure, jsonOptions));
        }
        catch (Exception ex)
        {
            try
            {
                Console.Error.WriteLine(ExceptionFormatting.SafeFormat(ex));
                Console.Error.WriteLine(message);
            }
            catch
            {
                // 最后兜底。
            }
        }
    }
}
