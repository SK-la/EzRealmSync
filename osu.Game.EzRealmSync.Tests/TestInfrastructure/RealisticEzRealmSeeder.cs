#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;
using osu.Game.Scoring;

namespace osu.Game.EzRealmSync.Tests.TestInfrastructure
{
    /// <summary>
    /// 造一份「真实感的当前 Ez 库」：先落一份当前 schema 的空库，再把官方样本里的真实对象动态同步进来，
    /// 最后往 Ez 扩展列写值。
    ///
    /// 用真实数据而不是手搭对象：链接、嵌入、文件用法这些结构手搭最容易漏，漏了对照也跟着漏。
    /// 动态侧与 typed 侧的多组对照测试（整表导出、数据页浏览）共用这一份种子，避免各自造出不同形状的库。
    /// </summary>
    internal static class RealisticEzRealmSeeder
    {
        public static string NewRoot(string name)
        {
            string root = Path.Combine(Path.GetTempPath(), "EzRealmSyncSeeder", name + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        public static void Cleanup(string root)
        {
            RealmNativeLifetime.Flush();

            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch
            {
                // 清理失败不影响断言结果。
            }
        }

        /// <summary>用 bundled Ez 模型落一份当前 schema 的空库；这是「当前 Ez 客户端」写出的等价产物。</summary>
        public static void CreateCurrentEzRealm(string path, int schema)
        {
            RealmNativeLifetime.CreateEmptyRealmFile(path, (ulong)schema);

            using (var access = RealmAccessGateway.OpenForMutation(path, schema))
                access.Run(_ => { });

            RealmNativeLifetime.Flush();
        }

        /// <summary>从官方样本同步一批真实对象进来（谱面集 / 难度 / 成绩 / 收藏夹 / 皮肤）。</summary>
        public static void SeedFromOfficialSample(string targetPath, int targetSchema)
        {
            RealmSampleInfo? sample = RealmSampleFixture.GetAllSamples()
                                                        .FirstOrDefault(s => s.RealmFileExists && s.DiskSchemaKind == nameof(RealmDiskSchemaKind.PpyClient));

            if (sample == null)
                Assert.Ignore("未放置官方样本，跳过基于真实数据的对照。");

            var source = DynamicBaselineReader.ReadDiffSnapshot(
                sample.RealmFilePath,
                [EntityKind.BeatmapSet, EntityKind.Score, EntityKind.BeatmapCollection, EntityKind.Skin]);

            var ids = new List<Guid>();

            foreach (EntityKind kind in new[] { EntityKind.BeatmapSet, EntityKind.Score, EntityKind.BeatmapCollection, EntityKind.Skin })
                ids.AddRange(source.Enumerate(kind).Take(3).Select(e => e.Id));

            if (ids.Count == 0)
                Assert.Ignore("官方样本里没有可同步的基线对象。");

            var bundle = DynamicBaselineReader.ExportByIds(sample.RealmFilePath, ids);

            if (bundle.Scores.Count == 0)
            {
                // 成绩是独立的一大块（链接、列表、嵌入用户），样本没有就只能少比一块。
                TestContext.Out.WriteLine("官方样本没有可同步的成绩，本次对照不覆盖 Score。");
            }

            DynamicBaselineFileCopier.CopyMissing(sample.RealmFilePath, targetPath, bundle);

            var result = DynamicBaselineWriter.Apply(
                new ApplyRequest { ItemIds = ids, CreateBackup = false },
                bundle,
                targetPath);

            Assert.That(result.AppliedCount, Is.GreaterThan(0), "官方 → Ez 的动态同步没有落任何对象。");
            RealmNativeLifetime.Flush();
        }

        /// <summary>往 Ez 扩展列写真实值：这些列官方样本里没有，只能 typed 写。</summary>
        public static void WriteEzOnlyValues(string path, int schema)
        {
            using var access = RealmAccessGateway.OpenForMutation(path, schema);

            access.Write(realm =>
            {
                foreach (var set in realm.All<BeatmapSetInfo>())
                {
                    set.ExternalContentRoot = @"D:\EzExternal\parity";
                    set.HostingKindInt = (int)BeatmapSetHostingKind.External;
                }

                foreach (var beatmap in realm.All<BeatmapInfo>())
                {
                    beatmap.XxyStarRating = 6.5;
                    beatmap.PerformancePoints = 210;
                }

                foreach (var score in realm.All<ScoreInfo>())
                {
                    score.ManiaHitMode = 4;
                    score.SessionAccuracyCutoffA = 0.25;
                }
            });

            RealmNativeLifetime.Flush();
        }
    }
}
#endif
