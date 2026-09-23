using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Realm.Dynamic;
using osu.Game.EzRealmSync.Tests.TestInfrastructure;
using osu.Game.Scoring;
using Realms;

namespace osu.Game.EzRealmSync.Tests
{
    [TestFixture]
    public class DynamicBaselineSyncTest
    {
        [Test]
        public void Probe_and_sync_do_not_change_schema_and_copy_baseline_set()
        {
            string worker = OfficialWorkerProcess.ResolveWorkerExecutablePathForTests();
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

                var snapshot = DynamicBaselineReader.ReadDiffSnapshot(sourcePath);
                Assert.That(snapshot.Entities.Any(e => e.EntityKind == EntityKind.BeatmapSet && e.Id == setId), Is.True);

                var bundle = DynamicBaselineReader.ExportByIds(sourcePath, [setId]);
                Assert.That(bundle.BeatmapSets, Has.Count.EqualTo(1));
                Assert.That(bundle.BeatmapSets[0].Beatmaps, Has.Count.EqualTo(1));

                var result = DynamicBaselineWriter.Apply(
                    new ApplyRequest { ItemIds = [setId], CreateBackup = false },
                    bundle,
                    targetPath);

                Assert.That(result.AppliedCount, Is.EqualTo(1));
                Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(targetPath), Is.EqualTo(51));
                Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(sourcePath), Is.EqualTo(51));

                using var verify = DynamicRealmSession.OpenDynamic(targetPath, readOnly: true);
                var copied = DynamicRealmAccess.Find(verify.Realm, OfficialBaselineSchema.BeatmapSet, setId);
                Assert.That(copied, Is.Not.Null);
                Assert.That(DynamicRealmAccess.GetString(copied, "Hash"), Is.EqualTo("set-hash"));

                assertDynamicOpenPreservesSchema(targetPath, 51);

                // 产物必须是一份纯官方库：schema 里不许出现 Ez 表或 Ez 列（官方客户端才不会认错）。
                using (var produced = DynamicRealmSession.OpenDynamic(targetPath, readOnly: true))
                    Assert.That(RealmSchemaSnapshotClassifier.HasEzFingerprint(DynamicSchemaReader.Read(produced)), Is.False, "产物里出现了 Ez 表或 Ez 列。");
            }
            finally
            {
                RealmNativeLifetime.Flush();
                tryDelete(root);
            }
        }

        [Test]
        public void Apply_refreshes_target_file_timestamp()
        {
            string worker = OfficialWorkerProcess.ResolveWorkerExecutablePathForTests();
            if (!File.Exists(worker))
                Assert.Ignore($"OfficialWrite Worker 未复制到测试输出：{worker}");

            string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "dyn-sync-stamp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            string sourcePath = Path.Combine(root, "source.realm");
            string targetPath = Path.Combine(root, "target.realm");
            Guid setId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
            Guid beatmapId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
            DateTime stale = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            try
            {
                createOfficialRealmViaWorker(sourcePath, 51, setId, beatmapId, "Stamped Song");
                createEmptyOfficialRealmViaWorker(targetPath, 51);

                // 模拟 Realm 提交后的状态：Windows 不因 mmap 写页更新 LastWriteTime，这里先把它按回过去。
                File.SetLastWriteTimeUtc(targetPath, stale);

                var bundle = DynamicBaselineReader.ExportByIds(sourcePath, [setId]);
                var result = DynamicBaselineWriter.Apply(new ApplyRequest { ItemIds = [setId], CreateBackup = false }, bundle, targetPath);

                Assert.That(result.AppliedCount, Is.EqualTo(1));
                Assert.That(
                    File.GetLastWriteTimeUtc(targetPath),
                    Is.GreaterThan(stale.AddMinutes(1)),
                    "写入后目标文件时间戳没跟上：用户看文件日期会以为同步没生效。");

                // 什么都没写（选中的条目不在源库里）时不该假装动过文件。
                File.SetLastWriteTimeUtc(targetPath, stale);
                DynamicBaselineWriter.Apply(new ApplyRequest { ItemIds = [Guid.NewGuid()], CreateBackup = false }, new RealmSyncApplyBundle(), targetPath);

                Assert.That(File.GetLastWriteTimeUtc(targetPath), Is.EqualTo(stale), "空写入不该更新目标文件时间戳。");
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
            string worker = OfficialWorkerProcess.ResolveWorkerExecutablePathForTests();
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

                var bundle = DynamicBaselineReader.ExportByIds(sourcePath, [beatmapId]);
                Assert.That(bundle.Beatmaps, Has.Count.EqualTo(1));
                Assert.That(bundle.Beatmaps[0].BeatmapSetID, Is.EqualTo(setId));

                var result = DynamicBaselineWriter.Apply(
                    new ApplyRequest { ItemIds = [beatmapId], CreateBackup = false },
                    bundle,
                    targetPath);

                Assert.That(result.AppliedCount, Is.EqualTo(1));
                Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(targetPath), Is.EqualTo(51));

                using var verify = DynamicRealmSession.OpenDynamic(targetPath, readOnly: true);
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
            string worker = OfficialWorkerProcess.ResolveWorkerExecutablePathForTests();
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

                var bundle = DynamicBaselineReader.ExportByIds(sourcePath, [beatmapId]);

                Assert.Throws<InvalidOperationException>(() => DynamicBaselineWriter.Apply(
                    new ApplyRequest { ItemIds = [beatmapId], CreateBackup = false },
                    bundle,
                    targetPath));

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
            string worker = OfficialWorkerProcess.ResolveWorkerExecutablePathForTests();
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

                var bundle = DynamicBaselineReader.ExportByIds(sourcePath, [scoreId]);
                Assert.That(bundle.Scores, Has.Count.EqualTo(1), "源库没有导出成绩。");
                Assert.That(bundle.Scores[0].BeatmapHash, Is.EqualTo("bm-hash"));

                var result = DynamicBaselineWriter.Apply(
                    new ApplyRequest { ItemIds = [scoreId], CreateBackup = false },
                    bundle,
                    targetPath);

                Assert.That(result.AppliedCount, Is.EqualTo(1));
                Assert.That(result.SkippedCount, Is.Zero);
                Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(targetPath), Is.EqualTo(51));

                using var verify = DynamicRealmSession.OpenDynamic(targetPath, readOnly: true);
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
            string worker = OfficialWorkerProcess.ResolveWorkerExecutablePathForTests();
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

                var bundle = DynamicBaselineReader.ExportByIds(sourcePath, [scoreId]);
                var result = DynamicBaselineWriter.Apply(
                    new ApplyRequest { ItemIds = [scoreId], CreateBackup = false },
                    bundle,
                    targetPath);

                Assert.That(result.AppliedCount, Is.Zero);
                Assert.That(result.SkippedCount, Is.EqualTo(1));
                Assert.That(result.SkipReasons, Has.Some.Contains("谱面"), "跳过原因没有点明缺谱面。");

                using var verify = DynamicRealmSession.OpenDynamic(targetPath, readOnly: true);
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
            string worker = OfficialWorkerProcess.ResolveWorkerExecutablePathForTests();
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

                var bundle = DynamicBaselineReader.ExportByIds(sourcePath, [scoreId]);
                Assert.That(bundle.Scores[0].RulesetShortName, Is.EqualTo("bms"));

                var result = DynamicBaselineWriter.Apply(
                    new ApplyRequest { ItemIds = [scoreId], CreateBackup = false },
                    bundle,
                    targetPath);

                Assert.That(result.AppliedCount, Is.Zero);
                Assert.That(result.SkippedCount, Is.EqualTo(1));
                Assert.That(result.SkipReasons, Has.Some.Contains("bms"));

                using var verify = DynamicRealmSession.OpenDynamic(targetPath, readOnly: true);
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
            // Ez 目标（磁盘版本 52010）：Ez 专用规则集的成绩在 Ez 侧必须完整保留——过滤只针对官方目标。
            string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "dyn-score-ezrs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            string sourcePath = Path.Combine(root, "source.realm");
            string targetPath = Path.Combine(root, "target.realm");
            Guid setId = Guid.Parse("10000000-0000-0000-0000-000000000004");
            Guid beatmapId = Guid.Parse("20000000-0000-0000-0000-000000000004");
            Guid scoreId = Guid.Parse("30000000-0000-0000-0000-000000000004");

            try
            {
                if (!File.Exists(OfficialWorkerProcess.ResolveWorkerExecutablePathForTests()))
                    Assert.Ignore("OfficialWrite Worker 未复制到测试输出，无法造带 bms 成绩的源库。");

                createOfficialRealmViaWorker(sourcePath, 51, setId, beatmapId, "BMS Song", scoreId, rulesetShortName: "bms");
                RealisticEzRealmSeeder.CreateCurrentEzRealm(targetPath, RealmAccess.EzFileSchemaVersion);

                // 先把谱面集同步进 Ez 目标（会落 bms 规则集行与难度），再同步成绩：目标已有该规则集。
                DynamicBaselineWriter.Apply(
                    new ApplyRequest { ItemIds = [setId], CreateBackup = false },
                    DynamicBaselineReader.ExportByIds(sourcePath, [setId]),
                    targetPath);

                var bundle = DynamicBaselineReader.ExportByIds(sourcePath, [scoreId]);
                var result = DynamicBaselineWriter.Apply(
                    new ApplyRequest { ItemIds = [scoreId], CreateBackup = false },
                    bundle,
                    targetPath);

                Assert.That(result.AppliedCount, Is.EqualTo(1), "Ez 目标下 Ez 专用规则集的成绩必须写入。");
                Assert.That(result.SkippedCount, Is.Zero);
            }
            finally
            {
                RealmNativeLifetime.Flush();
                tryDelete(root);
            }
        }

        [Test]
        public void Score_sync_filters_ez_only_ruleset_score_even_when_the_official_target_has_the_ruleset()
        {
            // 官方目标：Ez 专用规则集的成绩官方还原不了，目标里恰好有同名规则集行也不写——
            // 官方客户端只会把它显示成「不可用规则集」的成绩。
            string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "dyn-score-ezrs-official-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            string sourcePath = Path.Combine(root, "source.realm");
            string targetPath = Path.Combine(root, "target.realm");
            Guid setId = Guid.Parse("10000000-0000-0000-0000-000000000005");
            Guid beatmapId = Guid.Parse("20000000-0000-0000-0000-000000000005");
            Guid scoreId = Guid.Parse("30000000-0000-0000-0000-000000000005");

            try
            {
                if (!File.Exists(OfficialWorkerProcess.ResolveWorkerExecutablePathForTests()))
                    Assert.Ignore("OfficialWrite Worker 未复制到测试输出，无法造官方目标库。");

                createOfficialRealmViaWorker(sourcePath, 51, setId, beatmapId, "BMS Song", scoreId, rulesetShortName: "bms");
                createOfficialRealmViaWorker(targetPath, 51, setId, beatmapId, "BMS Song", rulesetShortName: "bms");

                var result = DynamicBaselineWriter.Apply(
                    new ApplyRequest { ItemIds = [scoreId], CreateBackup = false },
                    DynamicBaselineReader.ExportByIds(sourcePath, [scoreId]),
                    targetPath);

                Assert.That(result.AppliedCount, Is.Zero, "Ez 专用规则集的成绩不该写进官方库。");
                Assert.That(result.SkipReasons, Has.Some.Contains("bms"));

                using var verify = DynamicRealmSession.OpenDynamic(targetPath, readOnly: true);
                Assert.That(DynamicRealmAccess.Find(verify.Realm, OfficialBaselineSchema.Score, scoreId), Is.Null, "官方目标里出现了不该有的成绩。");
            }
            finally
            {
                RealmNativeLifetime.Flush();
                tryDelete(root);
            }
        }

        [Test]
        public void Score_sync_filters_ez_judgement_and_ez_mod_scores_only_for_official_targets()
        {
            string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "dyn-score-official-policy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            string sourcePath = Path.Combine(root, "source.realm");
            string officialPath = Path.Combine(root, "official.realm");
            string ezPath = Path.Combine(root, "ez.realm");

            Guid setId = Guid.Parse("10000000-0000-0000-0000-000000000006");
            Guid beatmapId = Guid.Parse("20000000-0000-0000-0000-000000000006");
            Guid ezModeScoreId = Guid.Parse("30000000-0000-0000-0000-000000000006");
            Guid ezModScoreId = Guid.Parse("30000000-0000-0000-0000-000000000007");
            Guid plainScoreId = Guid.Parse("30000000-0000-0000-0000-000000000008");

            try
            {
                if (!File.Exists(OfficialWorkerProcess.ResolveWorkerExecutablePathForTests()))
                    Assert.Ignore("OfficialWrite Worker 未复制到测试输出，无法造官方目标库。");

                seedEzSourceWithEzScores(sourcePath, setId, beatmapId, ezModeScoreId, ezModScoreId, plainScoreId);

                var bundle = DynamicBaselineReader.ExportByIds(sourcePath, [ezModeScoreId, ezModScoreId, plainScoreId]);

                createOfficialRealmViaWorker(officialPath, 51, setId, beatmapId, "Policy Song", rulesetShortName: "osu");

                var officialResult = DynamicBaselineWriter.Apply(
                    new ApplyRequest { ItemIds = [ezModeScoreId, ezModScoreId, plainScoreId], CreateBackup = false },
                    bundle,
                    officialPath);

                Assert.Multiple(() =>
                {
                    Assert.That(officialResult.AppliedCount, Is.EqualTo(1), "官方目标只该收下普通成绩。");
                    Assert.That(officialResult.SkipReasons, Has.Some.Contains("Ez 判定语义"));
                    Assert.That(officialResult.SkipReasons, Has.Some.Contains("mod"));
                });

                using (var verify = DynamicRealmSession.OpenDynamic(officialPath, readOnly: true))
                {
                    Assert.That(DynamicRealmAccess.Find(verify.Realm, OfficialBaselineSchema.Score, plainScoreId), Is.Not.Null, "普通成绩没写进去。");
                    Assert.That(DynamicRealmAccess.Find(verify.Realm, OfficialBaselineSchema.Score, ezModeScoreId), Is.Null, "Ez 判定语义的成绩写进了官方库。");
                    Assert.That(DynamicRealmAccess.Find(verify.Realm, OfficialBaselineSchema.Score, ezModScoreId), Is.Null, "带 Ez 专用 mod 的成绩写进了官方库。");
                }

                // 同一个 bundle 写进 Ez 目标：三条都该保留（过滤只在官方目标生效）。
                RealisticEzRealmSeeder.CreateCurrentEzRealm(ezPath, RealmAccess.EzFileSchemaVersion);

                DynamicBaselineWriter.Apply(
                    new ApplyRequest { ItemIds = [setId], CreateBackup = false },
                    DynamicBaselineReader.ExportByIds(sourcePath, [setId]),
                    ezPath);

                var ezResult = DynamicBaselineWriter.Apply(
                    new ApplyRequest { ItemIds = [ezModeScoreId, ezModScoreId, plainScoreId], CreateBackup = false },
                    bundle,
                    ezPath);

                Assert.That(ezResult.AppliedCount, Is.EqualTo(3), "Ez 目标不该过滤 Ez 判定语义与 Ez 专用 mod 的成绩。");
            }
            finally
            {
                RealmNativeLifetime.Flush();
                tryDelete(root);
            }
        }

        [Test]
        public void Set_sync_filters_externally_hosted_sets_only_for_official_targets()
        {
            string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "dyn-set-external-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            string sourcePath = Path.Combine(root, "source.realm");
            string officialPath = Path.Combine(root, "official.realm");
            string ezPath = Path.Combine(root, "ez.realm");

            Guid setId = Guid.Parse("10000000-0000-0000-0000-000000000007");
            Guid beatmapId = Guid.Parse("20000000-0000-0000-0000-000000000007");

            try
            {
                if (!File.Exists(OfficialWorkerProcess.ResolveWorkerExecutablePathForTests()))
                    Assert.Ignore("OfficialWrite Worker 未复制到测试输出，无法造官方目标库。");

                seedExternalHostedSet(sourcePath, setId, beatmapId);
                createEmptyOfficialRealmViaWorker(officialPath, 51);

                var bundle = DynamicBaselineReader.ExportByIds(sourcePath, [setId]);
                Assert.That(bundle.BeatmapSets[0].ExternallyHosted, Is.True, "源谱面集没有带上外部托管标记，过滤无从判断。");

                var officialResult = DynamicBaselineWriter.Apply(
                    new ApplyRequest { ItemIds = [setId], CreateBackup = false },
                    bundle,
                    officialPath);

                Assert.That(officialResult.AppliedCount, Is.Zero, "外部托管的谱面集内容在 Ez 的磁盘目录，官方读不到。");
                Assert.That(officialResult.SkipReasons, Has.Some.Contains("外部目录"));

                // Ez 目标：外部托管的集必须照常写入（Ez 客户端读得到那个目录）。
                RealisticEzRealmSeeder.CreateCurrentEzRealm(ezPath, RealmAccess.EzFileSchemaVersion);

                var ezResult = DynamicBaselineWriter.Apply(
                    new ApplyRequest { ItemIds = [setId], CreateBackup = false },
                    bundle,
                    ezPath);

                Assert.That(ezResult.AppliedCount, Is.EqualTo(1), "Ez 目标不该过滤外部托管的谱面集。");
            }
            finally
            {
                RealmNativeLifetime.Flush();
                tryDelete(root);
            }
        }

        /// <summary>造一份 Ez 源库：一条普通成绩 + 一条 Ez 判定语义成绩 + 一条 Ez 专用 mod 成绩。</summary>
        private static void seedEzSourceWithEzScores(string path, Guid setId, Guid beatmapId, Guid ezModeScoreId, Guid ezModScoreId, Guid plainScoreId)
        {
            RealisticEzRealmSeeder.CreateCurrentEzRealm(path, RealmAccess.EzFileSchemaVersion);

            var bundle = new RealmSyncApplyBundle
            {
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
                                RulesetShortName = "osu",
                                Hash = "bm-hash",
                                MD5Hash = "md5",
                                Metadata = new OfficialBeatmapMetadataDto { Title = "Policy Song", Artist = "Artist" },
                            },
                        ],
                    },
                ],
                Scores =
                [
                    scoreDto(plainScoreId, "bm-hash", """[{"acronym":"HD"}]"""),
                    scoreDto(ezModeScoreId, "bm-hash", """[{"acronym":"HD"}]"""),
                    scoreDto(ezModScoreId, "bm-hash", """[{"acronym":"NCl"}]"""),
                ],
            };

            var result = DynamicBaselineWriter.Apply(
                new ApplyRequest { ItemIds = [setId, plainScoreId, ezModeScoreId, ezModScoreId], CreateBackup = false },
                bundle,
                path);

            Assert.That(result.SkipReasons, Is.Empty, "夹具写入被跳过了，测试前提不成立。");

            using var access = TypedRealmAccess.OpenForMutation(path, RealmAccess.EzFileSchemaVersion);

            access.Write(realm =>
            {
                var score = realm.All<ScoreInfo>().Single(s => s.ID == ezModeScoreId);
                score.ManiaHitMode = (int)EzEnumHitMode.O2Jam;
            });

            RealmNativeLifetime.Flush();
        }

        /// <summary>造一份 Ez 源库：一个外部托管的谱面集（内容在 Ez 的磁盘目录）。</summary>
        private static void seedExternalHostedSet(string path, Guid setId, Guid beatmapId)
        {
            RealisticEzRealmSeeder.CreateCurrentEzRealm(path, RealmAccess.EzFileSchemaVersion);

            var bundle = new RealmSyncApplyBundle
            {
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
                                RulesetShortName = "osu",
                                Hash = "bm-hash",
                                MD5Hash = "md5",
                                Metadata = new OfficialBeatmapMetadataDto { Title = "External Song", Artist = "Artist" },
                            },
                        ],
                    },
                ],
            };

            DynamicBaselineWriter.Apply(
                new ApplyRequest { ItemIds = [setId], CreateBackup = false },
                bundle,
                path);

            using (var access = TypedRealmAccess.OpenForMutation(path, RealmAccess.EzFileSchemaVersion))
            {
                access.Write(realm =>
                {
                    var set = realm.All<BeatmapSetInfo>().Single(s => s.ID == setId);
                    set.HostingKind = BeatmapSetHostingKind.External;
                    set.ExternalContentRoot = @"D:\EzExternal\sync";
                });
            }

            RealmNativeLifetime.Flush();
        }

        private static OfficialScoreDto scoreDto(Guid id, string beatmapHash, string modsJson) => new()
        {
            ID = id,
            BeatmapHash = beatmapHash,
            RulesetShortName = "osu",
            ClientVersion = "2026.917.0",
            Hash = "replay-" + id,
            TotalScore = 1_000_000,
            MaxCombo = 500,
            Accuracy = 0.99,
            Date = DateTimeOffset.UtcNow,
            User = new OfficialRealmUserDto { OnlineID = 2, Username = "player", CountryString = "CR" },
            ModsJson = modsJson,
            StatisticsJson = "{\"Great\":100}",
        };

        private static void createEmptyOfficialRealmViaWorker(string path, int schema)
        {
            OfficialWorkerProcess.Run(new OfficialConvertJob
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
            OfficialWorkerProcess.Run(new OfficialConvertJob
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

            OfficialWorkerProcess.Run(job);
        }

        private static void assertDynamicOpenPreservesSchema(string realmPath, int schema)
        {
            using var session = DynamicRealmSession.OpenDynamic(realmPath, readOnly: true);
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
