using NUnit.Framework;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Realm.Dynamic;
using osu.Game.EzRealmSync.Tests.TestInfrastructure;

namespace osu.Game.EzRealmSync.Tests
{
    /// <summary>
    /// 只读探测与浏览一律走 DynamicRealm：任何磁盘 schema 都能读，不按版本选路、不要求匹配的 DLL。
    /// </summary>
    [TestFixture]
    public class RealmAccessGatewayTest
    {
        [Test]
        public void ProbeSchema_reads_disk_header_without_opening_realm()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"gw_probe_{Guid.NewGuid():N}.realm");

            try
            {
                RealmNativeLifetime.CreateEmptyRealmFile(path, 51_006);
                Assert.That(RealmAccessGateway.ProbeSchema(path), Is.EqualTo(51_006));
            }
            finally
            {
                RealmNativeLifetime.DeleteRealmFiles(path);
            }
        }

        [Test]
        public void ReadDiffSnapshot_uses_dynamic_baseline_without_dll()
        {
            string root = Path.Combine(Path.GetTempPath(), "EzRealmSyncGatewayTests", Guid.NewGuid().ToString("N"));
            string path = Path.Combine(root, "client_51007.realm");
            Directory.CreateDirectory(root);

            try
            {
                RealmNativeLifetime.CreateEmptyRealmFile(path, 51_007);
                Assert.DoesNotThrow((Action)(() => RealmAccessGateway.ReadDiffSnapshot(path)));
            }
            finally
            {
                RealmNativeLifetime.DeleteRealmFiles(path);

                try
                {
                    if (Directory.Exists(root))
                        Directory.Delete(root, recursive: true);
                }
                catch
                {
                }
            }
        }

        [Test]
        public void ReadDiffSnapshot_reads_legacy_sample()
        {
            var sample = RealmSampleFixture.GetSample("ez-old");
            if (!sample.RealmFileExists)
                Assert.Ignore($"样本未放置 realm 文件：{sample.RealmFilePath}");

            Assert.That(RealmAccessGateway.ProbeSchema(sample.RealmFilePath), Is.GreaterThan(0), "读不到样本的 schema 版本。");
            Assert.DoesNotThrow((Action)(() => RealmAccessGateway.ReadDiffSnapshot(sample.RealmFilePath)));
        }

        [Test]
        public void OpenDynamic_reads_without_touching_the_disk_header()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"gw_dynamic_{Guid.NewGuid():N}.realm");

            try
            {
                RealmNativeLifetime.CreateEmptyRealmFile(path, 52_010);

                using (RealmAccessGateway.OpenDynamicForRead(path, out RealmSchemaSnapshot schema))
                    Assert.That(schema.ClassCount, Is.GreaterThanOrEqualTo(0));

                Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(path), Is.EqualTo(52_010), "只读打开不得改动文件头。");
            }
            finally
            {
                RealmNativeLifetime.DeleteRealmFiles(path);
            }
        }

        [Test]
        public void Production_services_do_not_reference_typed_osu_game_access()
        {
            // DLL 只允许出现在测试夹具里：产品工程不得再出现 typed 打开 / reader 选路。
            // 引擎之外，AppModel / Desktop 与 Contracts 同属产品面，一并扫。
            string repoRoot = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));

            string[] productProjects =
            [
                "osu.Game.EzRealmSync",
                "osu.Game.EzRealmSync.Contracts",
                "osu.EzRealmSync.AppModel",
                "osu.EzRealmSync.Desktop",
            ];

            string[] typedMarkers =
            [
                "RealmAccess.OpenWithoutMigration",
                "OfficialRealmAccess",
                "RealmAccessGateway.OpenFor",
                "RealmReaderRegistry",
            ];

            var offenders = new List<string>();

            foreach (string project in productProjects)
            {
                string projectDir = Path.Combine(repoRoot, project);
                Assert.That(Directory.Exists(projectDir), Is.True, projectDir);

                offenders.AddRange(Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories)
                                            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                                            .Where(path => typedMarkers.Any(marker => File.ReadAllText(path).Contains(marker, StringComparison.Ordinal)))
                                            .Select(p => Path.GetRelativePath(repoRoot, p)));
            }

            Assert.That(offenders, Is.Empty, $"产品工程仍引用 typed osu.Game 访问：{string.Join(", ", offenders)}");
        }

        [Test]
        public void Product_projects_do_not_pull_in_the_typed_game_package()
        {
            // 产品只认 Realm 与 osu.Framework；一旦有人把 ez2lazer.Game 加回来，
            // 「publish 里没有 osu.Game.dll」就会靠 prune 脚本硬删来兜底，而不是结构上成立。
            string repoRoot = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));

            string[] productProjects =
            [
                "osu.Game.EzRealmSync/osu.Game.EzRealmSync.csproj",
                "osu.Game.EzRealmSync.Contracts/osu.Game.EzRealmSync.Contracts.csproj",
                "osu.EzRealmSync.AppModel/osu.EzRealmSync.AppModel.csproj",
                "osu.EzRealmSync.Desktop/osu.EzRealmSync.Desktop.csproj",
            ];

            var offenders = productProjects
                            .Where(relative => PackageReferencePattern.IsMatch(File.ReadAllText(Path.Combine(repoRoot, relative))))
                            .ToList();

            Assert.That(offenders, Is.Empty, $"产品工程把 typed 游戏包加回来了：{string.Join(", ", offenders)}");
        }

        private static readonly System.Text.RegularExpressions.Regex PackageReferencePattern =
            new(@"PackageReference[^>]*ez2lazer\.Game", System.Text.RegularExpressions.RegexOptions.Compiled);

        [Test]
        public void LoadRealmSnapshot_does_not_call_OpenForMutation()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));
            string path = Path.Combine(repoRoot, "osu.Game.EzRealmSync", "Realm", "RealmRealmDataService.cs");
            Assert.That(File.ReadAllText(path), Does.Not.Contain("OpenForMutation"));
        }
    }
}
