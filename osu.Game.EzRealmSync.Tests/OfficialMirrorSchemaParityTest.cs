using NUnit.Framework;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Realm.Dynamic;
using osu.Game.EzRealmSync.Tests.TestInfrastructure;

namespace osu.Game.EzRealmSync.Tests
{
    /// <summary>
    /// 手抄的官方 schema 镜像（<c>osu.Game.EzRealmSync.OfficialSchema</c>，经 OfficialWrite Worker 落成
    /// 「官方参考库」）是测试侧的地基：造官方参考库、给「转回官方版」当 schema 事实来源都靠它。它也最容易
    /// 悄悄漂——官方加一列、给某列补上非空约束，镜像不跟着改就只能等某个用例偶然炸出来。缺
    /// <c>[Required]</c> 就是这么暴露的：产物被官方只读打开直接失败（<c>has been made required</c>）。
    ///
    /// 所以这里拿**官方包自带模型**当独立事实源，把参考库的存盘 schema 逐列钉死。比的是存盘 schema 而不是
    /// 模型 schema：模型侧多出来的反向链接列官方客户端根本不写进文件，混进来会得到假差异。
    ///
    /// 51 与 52 的差别只有「52 多一张 <c>RealmOnlineAsset</c> 表、<c>Score.BeatmapHash</c> 多一个索引」
    /// （已用官方 51 真库样本与官方 52 包对拍确认）；镜像的模型类在 51/52 之间共用一份，因此 51 只钉
    /// 版本映射，52 才逐列对拍。
    /// </summary>
    [TestFixture]
    public class OfficialMirrorSchemaParityTest
    {
        private const int official_upstream_51 = 51;
        private const int official_upstream_52 = 52;

        [Test]
        public void Mirror_52_matches_the_official_package_schema()
        {
            var real = OfficialDllVerifierProcess.DumpOfficialSchema();

            Assert.That(
                real.DeclaredSchemaVersion,
                Is.EqualTo(official_upstream_52),
                "官方包声明的版本与本用例假设不一致，本用例的参考版本要跟着改。");

            RealmSchemaSnapshot mirror = runMirror(official_upstream_52);

            Assert.That(
                mirror.FindDifferences(real.Snapshot),
                Is.Empty,
                "官方 52 的镜像 schema 与官方包自带 schema 不一致：该更新 osu.Game.EzRealmSync.OfficialSchema 了。");
        }

        [Test]
        public void Mirror_51_is_the_52_mirror_without_the_class_fifty_two_added()
        {
            RealmSchemaSnapshot mirror51 = runMirror(official_upstream_51);
            RealmSchemaSnapshot mirror52 = runMirror(official_upstream_52);

            // 51 的镜像 = 52 的镜像去掉 52 才有的表。这条只钉版本映射：51 逐列对不对由上面那条传递保证
            // （52 与官方包完全一致，而两边只差这一张表）。
            Assert.That(
                mirror51.FindDifferences(mirror52),
                Is.EquivalentTo(new[] { $"类 RealmOnlineAsset：另一份有、本份没有" }),
                "官方 51 参考库与 52 参考库的差别不止 52 新增的表。");
        }

        /// <summary>让 OfficialWrite Worker 按目标版本落一份官方参考库，读回它存盘的 schema。</summary>
        private static RealmSchemaSnapshot runMirror(int upstream)
        {
            if (!OfficialDllVerifierProcess.VerifierAvailable)
                Assert.Ignore($"DllVerifier 未构建：{OfficialDllVerifierProcess.ResolveVerifierPathForTests()}");

            string worker = OfficialWorkerProcess.ResolveWorkerExecutablePathForTests();

            if (!File.Exists(worker))
                Assert.Ignore($"OfficialWrite Worker 未复制到测试输出，无法造官方参考库：{worker}");

            string root = Path.Combine(Path.GetTempPath(), "EzRealmSyncMirrorSchema", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                string mirrorPath = Path.Combine(root, "official.realm");

                OfficialWorkerProcess.Run(new OfficialConvertJob
                {
                    TargetUpstreamSchema = upstream,
                    TargetRealmPath = mirrorPath,
                    Rulesets = [new OfficialRulesetDto { ShortName = "osu", OnlineID = 0, Name = "osu!" }],
                });

                Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(mirrorPath), Is.EqualTo(upstream), "参考库没落成目标版本。");

                return OfficialDllVerifierProcess.DumpFileSchema(mirrorPath).Snapshot;
            }
            finally
            {
                try
                {
                    Directory.Delete(root, recursive: true);
                }
                catch
                {
                    // 临时目录清理失败不影响验收结论。
                }
            }
        }
    }
}
