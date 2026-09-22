using NUnit.Framework;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Tests.TestInfrastructure;
using Realms;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class DynamicBaselineSyncTest
    {
        [Test]
        public void Probe_and_sync_do_not_change_schema_and_copy_baseline_set()
        {
            string worker = OfficialWriteProcessRunner.ResolveWorkerExecutablePathForTests();
            if (!File.Exists(worker))
                Assert.Ignore($"OfficialWrite Worker 未复制到测试输出：{worker}");

            string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "dyn-sync-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            string sourcePath = Path.Combine(root, "source.realm");
            string targetPath = Path.Combine(root, "target.realm");
            Guid setId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            Guid beatmapId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

            try
            {
                createOfficialRealmViaWorker(sourcePath, 51, setId, beatmapId, "Source Song");
                createEmptyOfficialRealmViaWorker(targetPath, 51);

                Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(sourcePath), Is.EqualTo(51));
                Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(targetPath), Is.EqualTo(51));

                var snapshot = DynamicBaselineReader.ReadDiffSnapshot(sourcePath, 51);
                Assert.That(snapshot.Entities.Any(e => e.EntityKind == EntityKind.BeatmapSet && e.Id == setId), Is.True);

                var bundle = DynamicBaselineReader.ExportByIds(sourcePath, 51, [setId]);
                Assert.That(bundle.BeatmapSets, Has.Count.EqualTo(1));
                Assert.That(bundle.BeatmapSets[0].Beatmaps, Has.Count.EqualTo(1));

                var result = DynamicBaselineWriter.Apply(
                    new ApplyRequest { ItemIds = [setId], CreateBackup = false },
                    bundle,
                    targetPath,
                    51);

                Assert.That(result.AppliedCount, Is.EqualTo(1));
                Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(targetPath), Is.EqualTo(51));
                Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(sourcePath), Is.EqualTo(51));

                using var verify = DynamicRealmSession.OpenPinned(targetPath, 51, readOnly: true);
                var copied = DynamicRealmAccess.Find(verify.Realm, OfficialBaselineSchema.BeatmapSet, setId);
                Assert.That(copied, Is.Not.Null);
                Assert.That(DynamicRealmAccess.GetString(copied, "Hash"), Is.EqualTo("set-hash"));

                assertPinnedDynamicOpenPreservesSchema(targetPath, 51);
                OfficialMirrorSchemaVerifier.Verify(targetPath, 51, 0);
            }
            finally
            {
                RealmNativeLifetime.Flush();
                tryDelete(root);
            }
        }

        [Test]
        public void Writer_does_not_assign_ez_only_columns()
        {
            Assert.That(OfficialBaselineSchema.EzOnlyPropertyNames, Does.Contain("XxyStarRating"));
            Assert.That(OfficialBaselineSchema.IsKnownProperty(OfficialBaselineSchema.Beatmap, "XxyStarRating"), Is.False);
        }

        [Test]
        public void Standalone_beatmap_sync_links_into_existing_set()
        {
            string worker = OfficialWriteProcessRunner.ResolveWorkerExecutablePathForTests();
            if (!File.Exists(worker))
                Assert.Ignore($"OfficialWrite Worker 未复制到测试输出：{worker}");

            string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "dyn-bm-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            string sourcePath = Path.Combine(root, "source.realm");
            string targetPath = Path.Combine(root, "target.realm");
            Guid setId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
            Guid beatmapId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

            try
            {
                createOfficialRealmViaWorker(sourcePath, 51, setId, beatmapId, "Source Song");
                createOfficialRealmWithEmptySetViaWorker(targetPath, 51, setId);

                var bundle = DynamicBaselineReader.ExportByIds(sourcePath, 51, [beatmapId]);
                Assert.That(bundle.Beatmaps, Has.Count.EqualTo(1));
                Assert.That(bundle.Beatmaps[0].BeatmapSetID, Is.EqualTo(setId));

                var result = DynamicBaselineWriter.Apply(
                    new ApplyRequest { ItemIds = [beatmapId], CreateBackup = false },
                    bundle,
                    targetPath,
                    51);

                Assert.That(result.AppliedCount, Is.EqualTo(1));
                Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(targetPath), Is.EqualTo(51));

                using var verify = DynamicRealmSession.OpenPinned(targetPath, 51, readOnly: true);
                var copied = DynamicRealmAccess.Find(verify.Realm, OfficialBaselineSchema.Beatmap, beatmapId);
                Assert.That(copied, Is.Not.Null);

                var parent = DynamicRealmAccess.Get<IRealmObjectBase>(copied, "BeatmapSet");
                Assert.That(parent, Is.Not.Null);
                Assert.That(DynamicRealmAccess.Get<Guid>(parent, "ID"), Is.EqualTo(setId));
                Assert.That(
                    DynamicRealmAccess.EnumerateObjects(parent, "Beatmaps").Select(b => DynamicRealmAccess.Get<Guid>(b, "ID")),
                    Does.Contain(beatmapId),
                    "目标谱面集的 Beatmaps 列表没有挂上该难度。");
            }
            finally
            {
                RealmNativeLifetime.Flush();
                tryDelete(root);
            }
        }

        [Test]
        public void Standalone_beatmap_sync_without_parent_set_reports_error()
        {
            string worker = OfficialWriteProcessRunner.ResolveWorkerExecutablePathForTests();
            if (!File.Exists(worker))
                Assert.Ignore($"OfficialWrite Worker 未复制到测试输出：{worker}");

            string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "dyn-bm-orphan-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            string sourcePath = Path.Combine(root, "source.realm");
            string targetPath = Path.Combine(root, "target.realm");
            Guid setId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
            Guid beatmapId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

            try
            {
                createOfficialRealmViaWorker(sourcePath, 51, setId, beatmapId, "Source Song");
                createEmptyOfficialRealmViaWorker(targetPath, 51);

                var bundle = DynamicBaselineReader.ExportByIds(sourcePath, 51, [beatmapId]);

                Assert.Throws<InvalidOperationException>(() => DynamicBaselineWriter.Apply(
                    new ApplyRequest { ItemIds = [beatmapId], CreateBackup = false },
                    bundle,
                    targetPath,
                    51));

                Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(targetPath), Is.EqualTo(51));
            }
            finally
            {
                RealmNativeLifetime.Flush();
                tryDelete(root);
            }
        }

        [Test]
        public void Score_sync_links_by_beatmap_hash_and_copies_fields()
        {
            string worker = OfficialWriteProcessRunner.ResolveWorkerExecutablePathForTests();
            if (!File.Exists(worker))
                Assert.Ignore($"OfficialWrite Worker 未复制到测试输出：{worker}");

            string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "dyn-score-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            string sourcePath = Path.Combine(root, "source.realm");
            string targetPath = Path.Combine(root, "target.realm");
            Guid setId = Guid.Parse("10000000-0000-0000-0000-000000000001");
            Guid beatmapId = Guid.Parse("20000000-0000-0000-0000-000000000001");
            Guid scoreId = Guid.Parse("30000000-0000-0000-0000-000000000001");

            try
            {
                createOfficialRealmViaWorker(sourcePath, 51, setId, beatmapId, "Score Song", scoreId);
                createOfficialRealmViaWorker(targetPath, 51, setId, beatmapId, "Score Song");

                var bundle = DynamicBaselineReader.ExportByIds(sourcePath, 51, [scoreId]);
                Assert.That(bundle.Scores, Has.Count.EqualTo(1), "源库没有导出成绩。");
                Assert.That(bundle.Scores[0].BeatmapHash, Is.EqualTo("bm-hash"));

                var result = DynamicBaselineWriter.Apply(
                    new ApplyRequest { ItemIds = [scoreId], CreateBackup = false },
                    bundle,
                    targetPath,
                    51);

                Assert.That(result.AppliedCount, Is.EqualTo(1));
                Assert.That(result.SkippedCount, Is.Zero);
                Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(targetPath), Is.EqualTo(51));

                using var verify = DynamicRealmSession.OpenPinned(targetPath, 51, readOnly: true);
                var copied = DynamicRealmAccess.Find(verify.Realm, OfficialBaselineSchema.Score, scoreId);
                Assert.That(copied, Is.Not.Null, "目标库没有同步过去的成绩。");
                Assert.That(DynamicRealmAccess.Get<long>(copied, "TotalScore"), Is.EqualTo(1234567));
                Assert.That(DynamicRealmAccess.GetString(copied, "Mods"), Is.EqualTo("[{\"acronym\":\"HD\"}]"));
                Assert.That(DynamicRealmAccess.GetString(copied, "Statistics"), Is.EqualTo("{\"Great\":100}"));
                Assert.That(DynamicRealmAccess.EnumerateValues<int>(copied, "Pauses").ToArray(), Is.EqualTo(new[] { 55, 120 }));
                Assert.That(DynamicRealmAccess.EnumerateObjects(copied, "Files").Any(), Is.True, "成绩没有 Replay 文件用法。");
                Assert.That(DynamicRealmAccess.Find(verify.Realm, OfficialBaselineSchema.File, "replay-hash"), Is.Not.Null);

                // 链接按 BeatmapHash 而不是 ID：目标库里已有同 Hash 的难度时就该挂上。
                var linkedBeatmap = DynamicRealmAccess.Get<IRealmObjectBase>(copied, "BeatmapInfo");
                Assert.That(linkedBeatmap, Is.Not.Null, "成绩没有链接到目标谱面。");
                Assert.That(DynamicRealmAccess.Get<Guid>(linkedBeatmap, "ID"), Is.EqualTo(beatmapId));

                var linkedUser = DynamicRealmAccess.Get<IRealmObjectBase>(copied, "User");
                Assert.That(linkedUser, Is.Not.Null);
                Assert.That(DynamicRealmAccess.GetString(linkedUser, "Username"), Is.EqualTo("player"));
            }
            finally
            {
                RealmNativeLifetime.Flush();
                tryDelete(root);
            }
        }

        [Test]
        public void Score_sync_skips_when_target_lacks_matching_beatmap()
        {
            string worker = OfficialWriteProcessRunner.ResolveWorkerExecutablePathForTests();
            if (!File.Exists(worker))
                Assert.Ignore($"OfficialWrite Worker 未复制到测试输出：{worker}");

            string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "dyn-score-skip-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            string sourcePath = Path.Combine(root, "source.realm");
            string targetPath = Path.Combine(root, "target.realm");
            Guid setId = Guid.Parse("10000000-0000-0000-0000-000000000002");
            Guid beatmapId = Guid.Parse("20000000-0000-0000-0000-000000000002");
            Guid scoreId = Guid.Parse("30000000-0000-0000-0000-000000000002");

            try
            {
                createOfficialRealmViaWorker(sourcePath, 51, setId, beatmapId, "Score Song", scoreId);
                createEmptyOfficialRealmViaWorker(targetPath, 51);

                var bundle = DynamicBaselineReader.ExportByIds(sourcePath, 51, [scoreId]);
                var result = DynamicBaselineWriter.Apply(
                    new ApplyRequest { ItemIds = [scoreId], CreateBackup = false },
                    bundle,
                    targetPath,
                    51);

                Assert.That(result.AppliedCount, Is.Zero);
                Assert.That(result.SkippedCount, Is.EqualTo(1));
                Assert.That(result.SkipReasons, Has.Some.Contains("谱面"), "跳过原因没有点明缺谱面。");

                using var verify = DynamicRealmSession.OpenPinned(targetPath, 51, readOnly: true);
                Assert.That(DynamicRealmAccess.Find(verify.Realm, OfficialBaselineSchema.Score, scoreId), Is.Null,
                    "目标缺谱面时不应写入悬空成绩。");
                Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(targetPath), Is.EqualTo(51));
            }
            finally
            {
                RealmNativeLifetime.Flush();
                tryDelete(root);
            }
        }

        [Test]
        public void Score_sync_skips_ez_only_ruleset_missing_on_target()
        {
            string worker = OfficialWriteProcessRunner.ResolveWorkerExecutablePathForTests();
            if (!File.Exists(worker))
                Assert.Ignore($"OfficialWrite Worker 未复制到测试输出：{worker}");

            string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "dyn-score-ruleset-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            string sourcePath = Path.Combine(root, "source.realm");
            string targetPath = Path.Combine(root, "target.realm");
            Guid setId = Guid.Parse("10000000-0000-0000-0000-000000000003");
            Guid beatmapId = Guid.Parse("20000000-0000-0000-0000-000000000003");
            Guid scoreId = Guid.Parse("30000000-0000-0000-0000-000000000003");

            try
            {
                createOfficialRealmViaWorker(sourcePath, 51, setId, beatmapId, "BMS Song", scoreId, rulesetShortName: "bms");
                createOfficialRealmViaWorker(targetPath, 51, setId, beatmapId, "BMS Song");

                var bundle = DynamicBaselineReader.ExportByIds(sourcePath, 51, [scoreId]);
                Assert.That(bundle.Scores[0].RulesetShortName, Is.EqualTo("bms"));

                var result = DynamicBaselineWriter.Apply(
                    new ApplyRequest { ItemIds = [scoreId], CreateBackup = false },
                    bundle,
                    targetPath,
                    51);

                Assert.That(result.AppliedCount, Is.Zero);
                Assert.That(result.SkippedCount, Is.EqualTo(1));
                Assert.That(result.SkipReasons, Has.Some.Contains("bms"));

                using var verify = DynamicRealmSession.OpenPinned(targetPath, 51, readOnly: true);
                Assert.That(DynamicRealmAccess.Find(verify.Realm, OfficialBaselineSchema.Ruleset, "bms"), Is.Null,
                    "不应往目标库凭空插 Ez 专用规则集行。");
                Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(targetPath), Is.EqualTo(51));
            }
            finally
            {
                RealmNativeLifetime.Flush();
                tryDelete(root);
            }
        }

        [Test]
        public void Score_sync_applies_ez_only_ruleset_when_target_already_has_it()
        {
            string worker = OfficialWriteProcessRunner.ResolveWorkerExecutablePathForTests();
            if (!File.Exists(worker))
                Assert.Ignore($"OfficialWrite Worker 未复制到测试输出：{worker}");

            string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "dyn-score-ezrs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            string sourcePath = Path.Combine(root, "source.realm");
            string targetPath = Path.Combine(root, "target.realm");
            Guid setId = Guid.Parse("10000000-0000-0000-0000-000000000004");
            Guid beatmapId = Guid.Parse("20000000-0000-0000-0000-000000000004");
            Guid scoreId = Guid.Parse("30000000-0000-0000-0000-000000000004");

            try
            {
                createOfficialRealmViaWorker(sourcePath, 51, setId, beatmapId, "BMS Song", scoreId, rulesetShortName: "bms");
                createOfficialRealmViaWorker(targetPath, 51, setId, beatmapId, "BMS Song", rulesetShortName: "bms");

                var bundle = DynamicBaselineReader.ExportByIds(sourcePath, 51, [scoreId]);
                var result = DynamicBaselineWriter.Apply(
                    new ApplyRequest { ItemIds = [scoreId], CreateBackup = false },
                    bundle,
                    targetPath,
                    51);

                Assert.That(result.AppliedCount, Is.EqualTo(1), "目标已有该规则集时应正常写入（Ez → Ez 场景）。");
                Assert.That(result.SkippedCount, Is.Zero);
            }
            finally
            {
                RealmNativeLifetime.Flush();
                tryDelete(root);
            }
        }

        private static void createEmptyOfficialRealmViaWorker(string path, int schema)
        {
            OfficialWriteProcessRunner.Run(new OfficialConvertJob
            {
                TargetUpstreamSchema = schema,
                TargetRealmPath = path,
                Rulesets =
                [
                    new OfficialRulesetDto { ShortName = "osu", OnlineID = 0, Name = "osu!" },
                ],
            });
        }

        private static void createOfficialRealmWithEmptySetViaWorker(string path, int schema, Guid setId)
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
                        ID = setId,
                        Hash = "set-hash",
                        DateAdded = DateTimeOffset.UtcNow,
                    },
                ],
            });
        }

        private static void createOfficialRealmViaWorker(
            string path,
            int schema,
            Guid setId,
            Guid beatmapId,
            string title,
            Guid? scoreId = null,
            string rulesetShortName = "osu")
        {
            var job = new OfficialConvertJob
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
                        ID = setId,
                        Hash = "set-hash",
                        DateAdded = DateTimeOffset.UtcNow,
                        Beatmaps =
                        [
                            new OfficialBeatmapDto
                            {
                                ID = beatmapId,
                                DifficultyName = "Normal",
                                RulesetShortName = rulesetShortName,
                                Hash = "bm-hash",
                                MD5Hash = "md5",
                                Metadata = new OfficialBeatmapMetadataDto
                                {
                                    Title = title,
                                    Artist = "Artist",
                                    Author = new OfficialRealmUserDto { Username = "mapper" },
                                },
                            },
                        ],
                    },
                ],
            };

            if (scoreId is { } id)
            {
                job.Scores.Add(new OfficialScoreDto
                {
                    ID = id,
                    BeatmapHash = "bm-hash",
                    RulesetShortName = rulesetShortName,
                    ClientVersion = "2026.917.0",
                    Hash = "score-hash",
                    TotalScore = 1234567,
                    TotalScoreWithoutMods = 1200000,
                    TotalScoreVersion = 30000000,
                    MaxCombo = 500,
                    Accuracy = 0.9876,
                    Date = DateTimeOffset.UtcNow,
                    PP = 123.45,
                    OnlineID = 987,
                    User = new OfficialRealmUserDto { OnlineID = 2, Username = "player", CountryString = "CR" },
                    ModsJson = "[{\"acronym\":\"HD\"}]",
                    StatisticsJson = "{\"Great\":100}",
                    MaximumStatisticsJson = "{\"Great\":120}",
                    RankInt = 3,
                    Combo = 400,
                    Pauses = [55, 120],
                    Files = [new OfficialNamedFileDto { Hash = "replay-hash", Filename = "replay.osr" }],
                });
            }

            OfficialWriteProcessRunner.Run(job);
        }

        private static void assertPinnedDynamicOpenPreservesSchema(string realmPath, int schema)
        {
            using var session = DynamicRealmSession.OpenPinned(realmPath, schema, readOnly: true);
            Assert.That(session.DiskSchemaVersion, Is.EqualTo(schema));
            Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(realmPath), Is.EqualTo(schema));
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
            }
        }
    }
}
