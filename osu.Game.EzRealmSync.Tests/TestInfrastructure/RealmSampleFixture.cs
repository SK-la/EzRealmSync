using System.Text.Json;

namespace osu.Game.EzRealmSync.Tests.TestInfrastructure
{
    public sealed class RealmSampleInfo
    {
        public required string Kind { get; init; }
        public required string SampleDirectory { get; init; }
        public required string ManifestPath { get; init; }
        public required string RealmFileName { get; init; }
        public required string RealmFilePath { get; init; }
        public required string DiskSchemaKind { get; init; }
        public required bool CanOpenWithoutMigration { get; init; }
        public bool RealmFileExists => File.Exists(RealmFilePath);
    }

    public sealed class WritableRealmSample : IDisposable
    {
        public required string TempDirectory { get; init; }
        public required string RealmFilePath { get; init; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(TempDirectory))
                    Directory.Delete(TempDirectory, recursive: true);
            }
            catch
            {
                // 忽略临时目录清理失败，避免污染测试主流程。
            }
        }
    }

    public static class RealmSampleFixture
    {
        private const string sample_root_relative = "TestResources/RealmSamples";

        public static IReadOnlyList<RealmSampleInfo> GetAllSamples() =>
            Directory.Exists(getSampleRoot())
                ? Directory.GetDirectories(getSampleRoot())
                           .Select(Path.GetFileName)
                           .Where(name => !string.IsNullOrWhiteSpace(name))
                           .Select(name => GetSample(name!))
                           .OrderBy(s => s.Kind, StringComparer.OrdinalIgnoreCase)
                           .ToList()
                : Array.Empty<RealmSampleInfo>();

        public static RealmSampleInfo GetSample(string kind)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(kind);

            string sampleDirectory = Path.Combine(getSampleRoot(), kind);
            string manifestPath = Path.Combine(sampleDirectory, "manifest.json");

            if (!File.Exists(manifestPath))
                throw new FileNotFoundException($"未找到样本 manifest：{manifestPath}");

            var manifest = JsonSerializer.Deserialize<RealmSampleManifest>(File.ReadAllText(manifestPath), json_options)
                           ?? throw new InvalidOperationException($"manifest 解析失败：{manifestPath}");

            if (manifest.Expected == null)
                throw new InvalidOperationException($"manifest 缺少 expected 段：{manifestPath}");

            string realmFileName = string.IsNullOrWhiteSpace(manifest.RealmFile) ? "client.realm" : manifest.RealmFile;
            string realmFilePath = Path.Combine(sampleDirectory, realmFileName);

            return new RealmSampleInfo
            {
                Kind = kind,
                SampleDirectory = sampleDirectory,
                ManifestPath = manifestPath,
                RealmFileName = realmFileName,
                RealmFilePath = realmFilePath,
                DiskSchemaKind = manifest.Expected.DiskSchemaKind ?? string.Empty,
                CanOpenWithoutMigration = manifest.Expected.CanOpenWithoutMigration,
            };
        }

        public static WritableRealmSample CreateWritableCopy(RealmSampleInfo sample)
        {
            if (!sample.RealmFileExists)
                throw new FileNotFoundException($"样本缺少 realm 文件：{sample.RealmFilePath}");

            string tempRoot = Path.Combine(
                Path.GetTempPath(),
                "EzRealmSyncTests",
                "RealmSamples",
                sample.Kind,
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            copyDirectory(sample.SampleDirectory, tempRoot);

            return new WritableRealmSample
            {
                TempDirectory = tempRoot,
                RealmFilePath = Path.Combine(tempRoot, sample.RealmFileName),
            };
        }

        private static string getSampleRoot()
        {
            string candidate = Path.Combine(AppContext.BaseDirectory, sample_root_relative);
            if (Directory.Exists(candidate))
                return candidate;

            string? current = TestContext.CurrentContext.TestDirectory;
            for (int i = 0; i < 8 && current != null; i++)
            {
                string probe = Path.Combine(current, sample_root_relative);
                if (Directory.Exists(probe))
                    return probe;
                current = Directory.GetParent(current)?.FullName;
            }

            return Path.Combine(TestContext.CurrentContext.TestDirectory, sample_root_relative);
        }

        private static void copyDirectory(string sourceDir, string targetDir)
        {
            foreach (string directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceDir, directory);
                Directory.CreateDirectory(Path.Combine(targetDir, relativePath));
            }

            foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceDir, file);
                string targetPath = Path.Combine(targetDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Copy(file, targetPath, overwrite: true);
            }
        }

        private static readonly JsonSerializerOptions json_options = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        private sealed class RealmSampleManifest
        {
            public string? RealmFile { get; init; }
            public RealmSampleExpected? Expected { get; init; }
        }

        private sealed class RealmSampleExpected
        {
            public string? DiskSchemaKind { get; init; }
            public bool CanOpenWithoutMigration { get; init; }
        }
    }
}
