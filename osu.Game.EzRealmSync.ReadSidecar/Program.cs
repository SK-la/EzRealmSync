using System.Text.Json;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Runtime;

namespace osu.Game.EzRealmSync.ReadSidecar
{
    internal static class Program
    {
        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public static int Main(string[] args)
        {
            installSidecarHost();

            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
            {
                Console.Error.WriteLine("Usage: EzRealmSync.ReadSidecar <read|browse|apply-export> <job.json> [result.json]");
                return 2;
            }

            string mode = args[0];
            string jobPath = Path.GetFullPath(args[1]);
            string resultPath = args.Length >= 3 && !string.IsNullOrWhiteSpace(args[2])
                ? Path.GetFullPath(args[2])
                : jobPath + ".result.json";

            try
            {
                return mode switch
                {
                    "read" => runRead(jobPath, resultPath),
                    "browse" => runBrowse(jobPath, resultPath),
                    "apply-export" => runApplyExport(jobPath, resultPath),
                    _ => writeFailure(resultPath, $"未知模式：{mode}") ?? 2
                };
            }
            catch (Exception ex)
            {
                writeFailure(resultPath, ExceptionFormatting.SafeFormat(ex));
                return 1;
            }
        }

        private static void installSidecarHost()
        {
            EzRealmSyncRuntimeLibLoader.InstallSidecarHost(EzRealmSyncBackend.ResolveRuntimeLibDirectory());
        }

        private static int runRead(string jobPath, string resultPath)
        {
            RealmReadJob job = JsonSerializer.Deserialize<RealmReadJob>(File.ReadAllText(jobPath), jsonOptions)
                               ?? throw new InvalidOperationException("job.json 为空或格式无效。");

            configureReaderProbe(job.ReaderLibDirectory, job.SharedLibDirectory);

            RealmReadResult result = ReadSidecarEngine.ReadDiffSnapshot(job);
            File.WriteAllText(resultPath, JsonSerializer.Serialize(result, jsonOptions));
            return result.Success ? 0 : 1;
        }

        private static int runBrowse(string jobPath, string resultPath)
        {
            RealmBrowseJob job = JsonSerializer.Deserialize<RealmBrowseJob>(File.ReadAllText(jobPath), jsonOptions)
                                 ?? throw new InvalidOperationException("job.json 为空或格式无效。");

            configureReaderProbe(job.ReaderLibDirectory, job.SharedLibDirectory);

            RealmBrowseResult result = ReadSidecarEngine.ReadBrowseSnapshot(job);
            File.WriteAllText(resultPath, JsonSerializer.Serialize(result, jsonOptions));
            return result.Success ? 0 : 1;
        }

        private static int runApplyExport(string jobPath, string resultPath)
        {
            RealmApplyExportJob job = JsonSerializer.Deserialize<RealmApplyExportJob>(File.ReadAllText(jobPath), jsonOptions)
                                      ?? throw new InvalidOperationException("job.json 为空或格式无效。");

            configureReaderProbe(job.ReaderLibDirectory, job.SharedLibDirectory);

            RealmApplyExportResult result = ReadSidecarEngine.ExportApplyBundle(job);
            File.WriteAllText(resultPath, JsonSerializer.Serialize(result, jsonOptions));
            return result.Success ? 0 : 1;
        }

        private static void configureReaderProbe(string readerLibDirectory, string? sharedLibDirectory)
        {
            if (!string.IsNullOrWhiteSpace(sharedLibDirectory) && Directory.Exists(sharedLibDirectory))
                EzRealmSyncRuntimeLibLoader.PrependProbeDirectory(Path.GetFullPath(sharedLibDirectory));

            if (string.IsNullOrWhiteSpace(readerLibDirectory) || !Directory.Exists(readerLibDirectory))
                throw new InvalidOperationException($"reader lib 目录无效：{readerLibDirectory}");

            EzRealmSyncRuntimeLibLoader.PrependProbeDirectory(Path.GetFullPath(readerLibDirectory));
        }

        private static int? writeFailure(string resultPath, string message)
        {
            try
            {
                var failure = new RealmReadResult
                {
                    Success = false,
                    ErrorMessage = message
                };
                File.WriteAllText(resultPath, JsonSerializer.Serialize(failure, jsonOptions));
                return null;
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(resultPath, "{\"success\":false,\"errorMessage\":\"" + escapeJsonString(message) + "\"}");
                    return null;
                }
                catch
                {
                    try
                    {
                        Console.Error.WriteLine(ExceptionFormatting.SafeFormat(ex));
                        Console.Error.WriteLine(message);
                    }
                    catch
                    {
                        // 最后兜底：stderr 也不可用时静默。
                    }

                    return 1;
                }
            }
        }

        private static string escapeJsonString(string value)
        {
            return value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal)
                .Replace("\t", "\\t", StringComparison.Ordinal);
        }
    }
}
