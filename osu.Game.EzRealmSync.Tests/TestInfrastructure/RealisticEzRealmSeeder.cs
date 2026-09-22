#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Realm.Dynamic;
using osu.Game.Scoring;
using Realms;

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

            using (var access = TypedRealmAccess.OpenForMutation(path, schema))
                access.Run(_ => { });

            RealmNativeLifetime.Flush();
        }

        /// <summary>
        /// 从官方样本同步一批真实对象进来（谱面集 / 难度 / 成绩 / 收藏夹 / 皮肤）。
        ///
        /// 分步同步而不是一次全取：成绩只有在它所属难度也进了目标库时才写得进去，所以先落谱面集，
        /// 再挑「难度已在库里」的成绩；收藏夹官方样本是空的，改从 Ez 样本借——收藏夹只是一串 MD5
        /// 字符串，不依赖难度是否在库，跨样本借用不影响真实性。
        /// </summary>
        public static void SeedFromOfficialSample(string targetPath, int targetSchema)
        {
            RealmSampleInfo? official = findSample(nameof(RealmDiskSchemaKind.PpyClient));

            if (official == null)
                Assert.Ignore("未放置官方样本，跳过基于真实数据的对照。");

            // 谱面集 + 难度 + 成绩：一次挑出「有成绩落在其上的谱面集」，再取这些集下的成绩。
            // 顺序不能反：成绩只有在它所属难度也进了目标库时才写得进去，先取成绩再随便挑集，
            // writer 会因为链接缺失跳过，对照测试就失去覆盖面。
            var (setIds, scoreIds) = pickSetsWithScores(official.RealmFilePath);

            Assert.That(setIds, Is.Not.Empty, "官方样本里没有可同步的谱面集。");

            apply(official.RealmFilePath, targetPath, setIds, DynamicBaselineReader.ExportByIds(official.RealmFilePath, setIds));

            if (scoreIds.Count > 0)
                apply(official.RealmFilePath, targetPath, scoreIds, DynamicBaselineReader.ExportByIds(official.RealmFilePath, scoreIds));
            else
                TestContext.Out.WriteLine("官方样本里没有「所属谱面集可同步」的成绩，本次对照不覆盖 Score。");

            // 皮肤。
            var skinIds = DynamicBaselineReader.ReadDiffSnapshot(official.RealmFilePath, [EntityKind.Skin])
                                               .Enumerate(EntityKind.Skin)
                                               .Take(3)
                                               .Select(e => e.Id)
                                               .ToList();

            if (skinIds.Count > 0)
                apply(official.RealmFilePath, targetPath, skinIds, DynamicBaselineReader.ExportByIds(official.RealmFilePath, skinIds));

            // 收藏夹：官方样本为空时借 Ez 样本。
            seedCollections(official, targetPath);

            RealmNativeLifetime.Flush();
        }

        /// <summary>
        /// 挑「有成绩落在其上的谱面集」以及这些集下的成绩（各最多 3 个），供一次完整链路使用。
        ///
        /// 用动态读而不是 diff 快照：快照里成绩的 <c>Hash</c> 是 replay hash，拿不到它挂在哪个难度上，
        /// 只有直接走 <c>BeatmapInfo.BeatmapSet</c> 链接才能保证「集 → 难度 → 成绩」三者齐备。
        /// </summary>
        private static (List<Guid> SetIds, List<Guid> ScoreIds) pickSetsWithScores(string sourcePath)
        {
            var setIds = new List<Guid>();
            var scoreIds = new List<Guid>();

            using (var session = RealmAccessGateway.OpenDynamicForRead(sourcePath, out RealmSchemaSnapshot schema))
            {
                foreach (IRealmObjectBase score in DynamicRowAccess.LiveRows(session, schema, OfficialBaselineSchema.Score))
                {
                    if (setIds.Count >= 3 && scoreIds.Count >= 3)
                        break;

                    if (DynamicRowAccess.Resolve(score, schema, "BeatmapInfo.BeatmapSet") is not IRealmObjectBase set)
                        continue;

                    if (DynamicRowAccess.Resolve(set, schema, "ID") is not Guid setId || setId == Guid.Empty)
                        continue;

                    if (!setIds.Contains(setId))
                    {
                        if (setIds.Count >= 3)
                            continue;

                        setIds.Add(setId);
                    }

                    if (DynamicRowAccess.Resolve(score, schema, "ID") is Guid scoreId && scoreId != Guid.Empty && !scoreIds.Contains(scoreId) && scoreIds.Count < 3)
                        scoreIds.Add(scoreId);
                }
            }

            if (setIds.Count == 0)
            {
                // 样本里没有可用的成绩链路：退回纯谱面集，成绩对照整体跳过。
                setIds.AddRange(DynamicBaselineReader.ReadDiffSnapshot(sourcePath, [EntityKind.BeatmapSet])
                                                     .Enumerate(EntityKind.BeatmapSet)
                                                     .Take(3)
                                                     .Select(e => e.Id));
            }

            return (setIds, scoreIds);
        }

        private static void seedCollections(RealmSampleInfo official, string targetPath)
        {
            RealmSampleInfo? source = official;

            if (DynamicBaselineReader.ReadDiffSnapshot(official.RealmFilePath, [EntityKind.BeatmapCollection])
                                     .Enumerate(EntityKind.BeatmapCollection)
                                     .Any() == false)
            {
                source = findSample(nameof(RealmDiskSchemaKind.EzExtended));

                if (source == null)
                {
                    TestContext.Out.WriteLine("官方样本没有收藏夹且未放置 Ez 样本，本次对照不覆盖 BeatmapCollection。");
                    return;
                }
            }

            var collectionIds = DynamicBaselineReader.ReadDiffSnapshot(source.RealmFilePath, [EntityKind.BeatmapCollection])
                                                     .Enumerate(EntityKind.BeatmapCollection)
                                                     .Take(3)
                                                     .Select(e => e.Id)
                                                     .ToList();

            if (collectionIds.Count == 0)
                return;

            apply(source.RealmFilePath, targetPath, collectionIds, DynamicBaselineReader.ExportByIds(source.RealmFilePath, collectionIds));
        }

        private static void apply(string sourcePath, string targetPath, IReadOnlyList<Guid> ids, RealmSyncApplyBundle bundle)
        {
            DynamicBaselineFileCopier.CopyMissing(sourcePath, targetPath, bundle);

            var result = DynamicBaselineWriter.Apply(
                new ApplyRequest { ItemIds = ids, CreateBackup = false },
                bundle,
                targetPath);

            Assert.That(result.AppliedCount, Is.GreaterThan(0), "官方 → Ez 的动态同步没有落任何对象。");
        }

        private static RealmSampleInfo? findSample(string diskSchemaKind) =>
            RealmSampleFixture.GetAllSamples()
                              .FirstOrDefault(s => s.RealmFileExists && s.DiskSchemaKind == diskSchemaKind);

        /// <summary>
        /// 往 Ez 扩展列写真实值：这些列官方样本里没有，只能 typed 写。
        ///
        /// 刻意只让**一个**谱面集外部托管、**一条**成绩是 Ez 判定语义、**一条**成绩带 Ez 专用 mod：
        /// 「转官方 / 往官方同步」的过滤要有被滤掉的与被留下的两侧样本，全滤或全留都测不出过滤是否生效。
        /// </summary>
        public static void WriteEzOnlyValues(string path, int schema)
        {
            using var access = TypedRealmAccess.OpenForMutation(path, schema);

            access.Write(realm =>
            {
                BeatmapSetInfo? externalSet = realm.All<BeatmapSetInfo>().FirstOrDefault();

                if (externalSet != null)
                {
                    externalSet.ExternalContentRoot = @"D:\EzExternal\parity";
                    externalSet.HostingKindInt = (int)BeatmapSetHostingKind.External;
                }

                foreach (var beatmap in realm.All<BeatmapInfo>())
                {
                    beatmap.XxyStarRating = 6.5;
                    beatmap.PerformancePoints = 210;
                }

                ScoreInfo? ezModeScore = realm.All<ScoreInfo>().FirstOrDefault();
                ScoreInfo? ezModScore = realm.All<ScoreInfo>().ToList().Skip(1).FirstOrDefault();

                foreach (var score in realm.All<ScoreInfo>())
                {
                    score.SessionAccuracyCutoffA = 0.25;

                    if (ezModeScore != null && score.ID == ezModeScore.ID)
                    {
                        score.ManiaHitMode = (int)EzEnumHitMode.O2Jam;
                        score.ManiaHealthMode = (int)EzEnumHealthMode.O2JamHard;
                    }

                    if (ezModScore != null && score.ID == ezModScore.ID)
                        score.ModsJson = """[{"acronym":"NCl"}]""";
                }
            });

            RealmNativeLifetime.Flush();
        }
    }
}
#endif
