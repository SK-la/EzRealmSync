#if HAS_EZ_OSU_GAME
using System.Diagnostics;
using System.Text.Json;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Errors;

namespace osu.Game.EzRealmSync.Realm
{
    public static class OfficialMirrorSchemaVerifier
    {
        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static void Verify(string realmPath, int targetUpstreamSchema, int sourceFileHashCount)
        {
            string tempRoot = EzRealmSyncDataPaths.CreateTempSubdirectory("official-verify-run");
            string jobPath = Path.Combine(tempRoot, "verify-job.json");
            string resultPath = Path.Combine(tempRoot, "verify-result.json");

            try
            {
                var payload = new
                {
                    job = new OfficialConvertJob
                    {
                        TargetUpstreamSchema = targetUpstreamSchema,
                        TargetRealmPath = Path.GetFullPath(realmPath),
                    },
                    sourceFileHashCount,
                };

                File.WriteAllText(jobPath, JsonSerializer.Serialize(payload, jsonOptions));

                string workerPath = OfficialWriteProcessRunner.ResolveWorkerExecutablePathForTests();
                if (!File.Exists(workerPath))
                {
                    throw new RealmUserOperationException(
                        RealmUserErrorKind.SchemaModelMismatch,
                        $"无法校验转官方产物：未找到 Worker（{workerPath}）。");
                }

                var psi = OfficialWriteProcessRunner.CreateWorkerStartInfo(workerPath, new[] { "--verify", jobPath, resultPath });
                psi.RedirectStandardOutput = false;
                psi.RedirectStandardError = false;

                using var process = Process.Start(psi)
                    ?? throw new InvalidOperationException("无法启动校验 Worker。");

                process.WaitForExit();

                if (!File.Exists(resultPath))
                {
                    throw new RealmUserOperationException(
                        RealmUserErrorKind.SchemaModelMismatch,
                        "转官方产物校验失败：Worker 无输出。");
                }

                using var doc = JsonDocument.Parse(File.ReadAllText(resultPath));
                if (!doc.RootElement.GetProperty("success").GetBoolean())
                {
                    string? message = doc.RootElement.TryGetProperty("errorMessage", out var msg) ? msg.GetString() : null;
                    throw new RealmUserOperationException(RealmUserErrorKind.SchemaModelMismatch, message ?? "转官方产物校验失败。");
                }
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
#endif
