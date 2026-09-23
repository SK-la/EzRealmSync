using System.Text.Json;
using osu.Game.EzRealmSync.Realm.Dynamic;
using Realms;
using Realms.Schema;

namespace osu.Game.EzRealmSync.Tests.TestInfrastructure
{
    /// <summary>
    /// 测试夹具：以独立进程跑 <c>EzRealmSync.DllVerifier</c>——唯一加载**真实官方包** <c>ppy.osu.Game</c>
    /// 的工程。产品工程不得引用本文件。
    ///
    /// 与 <see cref="OfficialWorkerProcess"/> 的区别：那个用的是 Ez 侧手抄的官方 schema 镜像（只证明
    /// 「镜像自洽」）；这里用的是官方包自带模型，判定等同于官方客户端。
    ///
    /// 走独立进程有两个硬原因：官方 osu.Game.dll 与 Ez 版同名，同进程加载必然撞；且验收器输出目录
    /// 里有整套官方依赖，不能并进测试输出。
    /// </summary>
    internal static class OfficialDllVerifierProcess
    {
        private static readonly JsonSerializerOptions json_options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        /// <summary>验收器是否已构建。未构建时相关测试应 Assert.Ignore 而不是失败。</summary>
        public static bool VerifierAvailable => File.Exists(ResolveVerifierPathForTests());

        public static string ResolveVerifierPathForTests()
        {
#if DEBUG
            const string configuration = "Debug";
#else
            const string configuration = "Release";
#endif
            const string target_framework = "net10.0";

            // 输出目录的层级随配置/框架变，写死 "../.." 迟早错位；从输出目录往上找仓库根再拼。
            string? repositoryRoot = findRepositoryRoot(AppContext.BaseDirectory);

            var candidates = new List<string>
            {
                Path.Combine(AppContext.BaseDirectory, "dll-verifier", "EzRealmSync.DllVerifier.exe"),
                Path.Combine(AppContext.BaseDirectory, "dll-verifier", "EzRealmSync.DllVerifier.dll"),
            };

            if (repositoryRoot != null)
            {
                candidates.Add(Path.Combine(repositoryRoot, "osu.Game.EzRealmSync.DllVerifier", "bin", configuration, target_framework, "EzRealmSync.DllVerifier.dll"));
                candidates.Add(Path.Combine(repositoryRoot, "osu.Game.EzRealmSync.DllVerifier", "bin", configuration, target_framework, "EzRealmSync.DllVerifier.exe"));
            }

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            return Path.Combine(AppContext.BaseDirectory, "dll-verifier", "EzRealmSync.DllVerifier.dll");
        }

        private static string? findRepositoryRoot(string start)
        {
            var directory = new DirectoryInfo(start);

            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "EzRealmSync.sln")))
                    return directory.FullName;

                directory = directory.Parent;
            }

            return null;
        }

        /// <summary>
        /// 官方模型的完整 schema，转换成产品侧同款结构以便逐列比对。
        /// <see cref="OfficialSchemaDump.DeclaredSchemaVersion"/> 是官方包自己写进文件头的号。
        /// </summary>
        public static OfficialSchemaDump DumpOfficialSchema() => toDump(run("dump-schema"));

        /// <summary>某份 .realm 文件里**实际存在**的 schema（动态打开，不掺进程模型）。</summary>
        public static OfficialSchemaDump DumpFileSchema(string realmPath) => toDump(run("dump-file-schema", Path.GetFullPath(realmPath)));

        /// <summary>
        /// 用官方 DLL 打开并按钉死版本读一遍。失败时把原因带回 <paramref name="error"/>，不抛异常。
        /// </summary>
        public static bool TryOpen(string realmPath, int pinnedSchemaVersion, out string? error, bool readOnly = true)
        {
            string fullPath = Path.GetFullPath(realmPath);
            string jobPath = Path.Combine(EzRealmSyncDataPaths.CreateTempSubdirectory("dll-verifier-job"), "job.json");

            try
            {
                File.WriteAllText(jobPath, JsonSerializer.Serialize(new
                {
                    realmPath = fullPath,
                    pinnedSchemaVersion,
                    readOnly,
                }, json_options));

                var outcome = run("verify-open", jobPath);
                error = outcome.Success ? null : outcome.Error ?? "官方 DLL 打不开，未给出原因。";
                return outcome.Success;
            }
            finally
            {
                ExternalProcessRunner.DeleteDirectoryQuietly(Path.GetDirectoryName(jobPath)!);
            }
        }

        private static VerificationOutcomeDto run(string mode, params string[] leadingArgs)
        {
            string verifierPath = ResolveVerifierPathForTests();
            if (!File.Exists(verifierPath))
                throw new InvalidOperationException($"未找到 DllVerifier：{verifierPath}");

            string tempRoot = EzRealmSyncDataPaths.CreateTempSubdirectory("dll-verifier");
            string resultPath = Path.Combine(tempRoot, "result.json");

            try
            {
                var args = new List<string> { mode };
                args.AddRange(leadingArgs);
                args.Add(resultPath);

                ProcessRunResult runResult = ExternalProcessRunner.Run(verifierPath, args, CancellationToken.None);

                if (!File.Exists(resultPath))
                    throw new InvalidOperationException(ExternalProcessRunner.DescribeFailure("DllVerifier", verifierPath, runResult, "未产出 result.json"));

                return JsonSerializer.Deserialize<VerificationOutcomeDto>(File.ReadAllText(resultPath), json_options)
                       ?? throw new InvalidOperationException("DllVerifier 结果 JSON 无效。");
            }
            finally
            {
                ExternalProcessRunner.DeleteDirectoryQuietly(tempRoot);
            }
        }

        private static OfficialSchemaDump toDump(VerificationOutcomeDto outcome)
        {
            if (!outcome.Success)
                throw new InvalidOperationException(outcome.Error ?? "DllVerifier 失败，未给出原因。");

            if (outcome.Classes == null)
                throw new InvalidOperationException("DllVerifier 没有返回 schema。");

            var classes = outcome.Classes
                                 .Select(c => new RealmClassSchema(
                                     c.Name,
                                     c.IsEmbedded,
                                     c.Properties
                                      .Select(p => new RealmPropertySchema(
                                          p.Name,
                                          (PropertyType)p.TypeCode,
                                          p.ObjectType ?? string.Empty,
                                          p.LinkOriginPropertyName,
                                          p.IsPrimaryKey,
                                          (IndexType)p.IndexTypeCode))
                                      .ToList()))
                                 .ToList();

            return new OfficialSchemaDump(new RealmSchemaSnapshot(classes), outcome.DeclaredSchemaVersion, outcome.OpenedSchemaVersion);
        }

        /// <summary>验收器导出的一份 schema，外加它自报的版本号。</summary>
        internal sealed record OfficialSchemaDump(RealmSchemaSnapshot Snapshot, int? DeclaredSchemaVersion, int? OpenedSchemaVersion);

        private sealed class VerificationOutcomeDto
        {
            public bool Success { get; set; }

            public string? Error { get; set; }

            public int? DeclaredSchemaVersion { get; set; }

            public int? ConfiguredSchemaVersion { get; set; }

            public int? OpenedSchemaVersion { get; set; }

            public Dictionary<string, int>? Counts { get; set; }

            public List<SchemaClassDto>? Classes { get; set; }
        }

        private sealed class SchemaClassDto
        {
            public string Name { get; set; } = string.Empty;

            public bool IsEmbedded { get; set; }

            public List<SchemaPropertyDto> Properties { get; set; } = new();
        }

        private sealed class SchemaPropertyDto
        {
            public string Name { get; set; } = string.Empty;

            public int TypeCode { get; set; }

            public string? ObjectType { get; set; }

            public string? LinkOriginPropertyName { get; set; }

            public bool IsPrimaryKey { get; set; }

            public int IndexTypeCode { get; set; }
        }
    }
}
