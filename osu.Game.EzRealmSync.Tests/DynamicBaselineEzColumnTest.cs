#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Tests.TestInfrastructure;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using Realms;

namespace osu.Game.EzRealmSync.Tests
{
    /// <summary>
    /// 同步写入当前 Ez 库（带 Ez 扩展列且列里有真实值）时，Ez 列必须保持原值。
    ///
    /// 这是「只写白名单列、不碰 Ez 列」这条不变量的**行为**验证：仅断言
    /// <see cref="OfficialBaselineSchema.EzOnlyPropertyNames"/> 的内容（见
    /// <c>Writer_does_not_assign_ez_only_columns</c>）不能证明磁盘上的值没被改动——
    /// 删行重建同样会让 Ez 列归零。
    /// </summary>
    [TestFixture]
    public class DynamicBaselineEzColumnTest
    {
        private static readonly Guid set_id = Guid.Parse("aa000000-0000-0000-0000-0000000000a1");
        private static readonly Guid beatmap_id = Guid.Parse("bb000000-0000-0000-0000-0000000000b1");
        private static readonly Guid score_id = Guid.Parse("cc000000-0000-0000-0000-0000000000c1");

        private const string external_root = @"D:\EzExternal\set-a1";
        private const double xxy_sr = 7.25;
        private const double performance_points = 321;
        private const int mania_hit_mode = 7;
        private const int mania_health_mode = 3;
        private const int last_applied_xxy_sr_version = 20260921;

        [Test]
        public void Standalone_beatmap_sync_keeps_ez_columns()
        {
            string worker = OfficialWriteProcessRunner.ResolveWorkerExecutablePathForTests();
            if (!File.Exists(worker))
                Assert.Ignore($"OfficialWrite Worker 未复制到测试输出：{worker}");

            string root = newRoot("ez-col-bm");
            int schema = RealmAccess.EzFileSchemaVersion;
            string sourcePath = Path.Combine(root, "source.realm");
            string targetPath = Path.Combine(root, "target.realm");

            try
            {
                createOfficialRealmViaWorker(sourcePath, 51);
                createCurrentEzRealm(targetPath, schema);

                // 先把谱面集、难度与成绩都落到 Ez 目标，再在 Ez 列上写真实值。
                applyFromSource(sourcePath, targetPath, [set_id, score_id]);
                writeEzOnlyValues(targetPath, schema);
                assertEzValuesPresent(targetPath, "写入 Ez 列后");

                // 单独同步难度：目标已有父谱面集，走就地更新分支。
                applyFromSource(sourcePath, targetPath, [beatmap_id]);

                assertEzValuesPreserved(targetPath, "单独同步难度后");
            }
            finally
            {
                RealmNativeLifetime.Flush();
                tryDelete(root);
            }
        }

        [Test]
        public void Set_and_score_overwrite_keeps_ez_columns()
        {
            string worker = OfficialWriteProcessRunner.ResolveWorkerExecutablePathForTests();
            if (!File.Exists(worker))
                Assert.Ignore($"OfficialWrite Worker 未复制到测试输出：{worker}");

            string root = newRoot("ez-col-set");
            int schema = RealmAccess.EzFileSchemaVersion;
            string sourcePath = Path.Combine(root, "source.realm");
            string targetPath = Path.Combine(root, "target.realm");

            try
            {
                createOfficialRealmViaWorker(sourcePath, 51);
                createCurrentEzRealm(targetPath, schema);

                applyFromSource(sourcePath, targetPath, [set_id, score_id]);
                writeEzOnlyValues(targetPath, schema);
                assertEzValuesPresent(targetPath, "写入 Ez 列后");

                // 冲突覆盖：同一个 ID 再同步一次（UI 上 Conflicted 走的正是这条）。
                applyFromSource(sourcePath, targetPath, [set_id, score_id]);

                assertEzValuesPreserved(targetPath, "覆盖同步谱面集与成绩后");
            }
            finally
            {
                RealmNativeLifetime.Flush();
                tryDelete(root);
            }
        }

        private static void createOfficialRealmViaWorker(string path, int schema)
        {
            OfficialWriteProcessRunner.Run(new OfficialConvertJob
            {
                TargetUpstreamSchema = schema,
                TargetRealmPath = path,
                Rulesets =
                [
                    new OfficialRulesetDto { ShortName = "osu", OnlineID = 0, Name = "osu!" },
                ],
                BeatmapSets =
                [
                    new OfficialBeatmapSetDto
                    {
                        ID = set_id,
                        Hash = "set-hash",
                        DateAdded = DateTimeOffset.UtcNow,
                        Beatmaps =
                        [
                            new OfficialBeatmapDto
                            {
                                ID = beatmap_id,
                                DifficultyName = "Normal",
                                RulesetShortName = "osu",
                                Hash = "bm-hash",
                                MD5Hash = "bm-md5",
                                Metadata = new OfficialBeatmapMetadataDto
                                {
                                    Title = "Ez Column Song",
                                    Artist = "Artist",
                                    Author = new OfficialRealmUserDto { Username = "mapper" },
                                },
                            },
                        ],
                    },
                ],
                Scores =
                [
                    new OfficialScoreDto
                    {
                        ID = score_id,
                        BeatmapHash = "bm-hash",
                        RulesetShortName = "osu",
                        ClientVersion = "2026.917.0",
                        Hash = "score-hash",
                        TotalScore = 1234567,
                        MaxCombo = 500,
                        Accuracy = 0.9876,
                        Date = DateTimeOffset.UtcNow,
                        User = new OfficialRealmUserDto { OnlineID = 2, Username = "player", CountryString = "CR" },
                        ModsJson = "[{\"acronym\":\"HD\"}]",
                        StatisticsJson = "{\"Great\":100}",
                    },
                ],
            });
        }

        /// <summary>用 bundled Ez 模型落一份当前 schema 的空库；这是「当前 Ez 客户端」写出的等价产物。</summary>
        private static void createCurrentEzRealm(string path, int schema)
        {
            RealmNativeLifetime.CreateEmptyRealmFile(path, (ulong)schema);

            using (var access = RealmAccessGateway.OpenForMutation(path, schema))
                access.Run(_ => { });

            RealmNativeLifetime.Flush();
        }

        private static void applyFromSource(string sourcePath, string targetPath, IReadOnlyList<Guid> ids)
        {
            var bundle = DynamicBaselineReader.ExportByIds(sourcePath, ids);
            Assert.That(bundle.BeatmapSets.Count + bundle.Beatmaps.Count + bundle.Scores.Count, Is.GreaterThan(0),
                "源库没有导出任何要同步的对象。");

            var result = DynamicBaselineWriter.Apply(
                new ApplyRequest { ItemIds = ids, CreateBackup = false },
                bundle,
                targetPath);

            Assert.That(result.AppliedCount + result.SkippedCount, Is.GreaterThan(0), "写入既没落任何对象、也没报跳过。");
        }

        /// <summary>用 typed 模型往 Ez 列写真实值——这些列在官方 schema 里不存在。</summary>
        private static void writeEzOnlyValues(string path, int schema)
        {
            using var access = RealmAccessGateway.OpenForMutation(path, schema);
            access.Write(realm =>
            {
                var set = realm.All<BeatmapSetInfo>().Single(s => s.ID == set_id);
                set.ExternalContentRoot = external_root;
                set.HostingKind = BeatmapSetHostingKind.External;

                var beatmap = realm.All<BeatmapInfo>().Single(b => b.ID == beatmap_id);
                beatmap.XxyStarRating = xxy_sr;
                beatmap.PerformancePoints = performance_points;
                beatmap.HasVideo = true;
                beatmap.HasStoryboard = false;

                var score = realm.All<ScoreInfo>().Single(s => s.ID == score_id);
                score.ManiaHitMode = mania_hit_mode;
                score.ManiaHealthMode = mania_health_mode;

                var ruleset = realm.All<RulesetInfo>().Single(r => r.ShortName == "osu");
                ruleset.LastAppliedXxySrVersion = last_applied_xxy_sr_version;
            });

            RealmNativeLifetime.Flush();
        }

        private static void assertEzValuesPresent(string path, string stage)
        {
            readEzValues(path, out var values);

            Assert.Multiple(() =>
            {
                Assert.That(values.ExternalContentRoot, Is.EqualTo(external_root), $"{stage}：ExternalContentRoot 没写进去。");
                Assert.That(values.HostingKind, Is.EqualTo((int)BeatmapSetHostingKind.External), $"{stage}：HostingKind 没写进去。");
                Assert.That(values.XxyStarRating, Is.EqualTo(xxy_sr), $"{stage}：XxyStarRating 没写进去。");
                Assert.That(values.ManiaHitMode, Is.EqualTo(mania_hit_mode), $"{stage}：ManiaHitMode 没写进去。");
                Assert.That(values.LastAppliedXxySrVersion, Is.EqualTo(last_applied_xxy_sr_version), $"{stage}：LastAppliedXxySrVersion 没写进去。");
            });
        }

        private static void assertEzValuesPreserved(string path, string stage)
        {
            readEzValues(path, out var values);

            Assert.Multiple(() =>
            {
                Assert.That(values.ExternalContentRoot, Is.EqualTo(external_root),
                    $"{stage}：谱面集的 Ez 列 ExternalContentRoot 被改动。");
                Assert.That(values.HostingKind, Is.EqualTo((int)BeatmapSetHostingKind.External),
                    $"{stage}：谱面集的 Ez 列 HostingKind 被改动。");
                Assert.That(values.XxyStarRating, Is.EqualTo(xxy_sr),
                    $"{stage}：难度的 Ez 列 XxyStarRating 被改动。");
                Assert.That(values.PerformancePoints, Is.EqualTo(performance_points),
                    $"{stage}：难度的 Ez 列 PerformancePoints 被改动。");
                Assert.That(values.HasVideo, Is.True, $"{stage}：难度的 Ez 列 HasVideo 被改动。");
                Assert.That(values.HasStoryboard, Is.False, $"{stage}：难度的 Ez 列 HasStoryboard 被改动。");
                Assert.That(values.ManiaHitMode, Is.EqualTo(mania_hit_mode),
                    $"{stage}：成绩的 Ez 列 ManiaHitMode 被改动。");
                Assert.That(values.ManiaHealthMode, Is.EqualTo(mania_health_mode),
                    $"{stage}：成绩的 Ez 列 ManiaHealthMode 被改动。");
                Assert.That(values.LastAppliedXxySrVersion, Is.EqualTo(last_applied_xxy_sr_version),
                    $"{stage}：规则集的 Ez 列 LastAppliedXxySrVersion 被改动。");
            });
        }

        /// <summary>用 DynamicRealm 读回 Ez 列，避免依赖 typed 模型的内存态。</summary>
        private static void readEzValues(string path, out EzValues values)
        {
            using var session = DynamicRealmSession.OpenDynamic(path, readOnly: true);

            var set = DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.BeatmapSet, set_id);
            var beatmap = DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.Beatmap, beatmap_id);
            var score = DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.Score, score_id);
            var ruleset = DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.Ruleset, "osu");

            Assert.That(set, Is.Not.Null, "目标库缺少谱面集。");
            Assert.That(beatmap, Is.Not.Null, "目标库缺少难度。");
            Assert.That(score, Is.Not.Null, "目标库缺少成绩。");
            Assert.That(ruleset, Is.Not.Null, "目标库缺少规则集。");

            values = new EzValues
            {
                ExternalContentRoot = DynamicRealmAccess.GetString(set, "ExternalContentRoot"),
                HostingKind = DynamicRealmAccess.Get<int>(set, "HostingKind"),
                XxyStarRating = DynamicRealmAccess.Get<double>(beatmap, "XxyStarRating"),
                PerformancePoints = DynamicRealmAccess.Get<double>(beatmap, "PerformancePoints"),
                HasVideo = DynamicRealmAccess.Get<bool?>(beatmap, "HasVideo") ?? false,
                HasStoryboard = DynamicRealmAccess.Get<bool?>(beatmap, "HasStoryboard") ?? true,
                ManiaHitMode = DynamicRealmAccess.Get<int>(score, "ManiaHitMode"),
                ManiaHealthMode = DynamicRealmAccess.Get<int>(score, "ManiaHealthMode"),
                LastAppliedXxySrVersion = DynamicRealmAccess.Get<int>(ruleset, "LastAppliedXxySrVersion"),
            };
        }

        private static string newRoot(string name)
        {
            string root = Path.Combine(Path.GetTempPath(), "EzRealmSyncEzColumn", name + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void tryDelete(string root)
        {
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

        private readonly record struct EzValues
        {
            public required string ExternalContentRoot { get; init; }
            public required int HostingKind { get; init; }
            public required double XxyStarRating { get; init; }
            public required double PerformancePoints { get; init; }
            public required bool HasVideo { get; init; }
            public required bool HasStoryboard { get; init; }
            public required int ManiaHitMode { get; init; }
            public required int ManiaHealthMode { get; init; }
            public required int LastAppliedXxySrVersion { get; init; }
        }
    }
}
#endif
