using NUnit.Framework;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Realm.Dynamic;
using osu.Game.EzRealmSync.Tests.TestInfrastructure;
using Realms;

namespace osu.Game.EzRealmSync.Tests
{
    /// <summary>
    /// 「转回官方版」的验收：一份真实感的 Ez 库收窄成官方库后，必须
    /// <list type="bullet">
    /// <item>schema 与官方逐列一致（Ez 表、Ez 列不残留）；</item>
    /// <item>公共表行数、关键格子的值与源库一致；</item>
    /// <item>能被官方 DLL 以 pinned 版本打开。</item>
    /// </list>
    /// 「能被官方打开」是这条功能的最终口径，所以最后一条不走 Ignore 之外的退路：Worker 不在就跳过，
    /// 在就必须通过。
    /// </summary>
    [TestFixture]
    public class DynamicOfficialConvertTest
    {
        private const int ez_schema = 52_010;
        private const int official_upstream = 52;

        private const string ez_only_class = "EzDanEstimate";
        private const string ez_only_beatmap_set_column = "ExternalContentRoot";
        private const string ez_only_score_column = "ManiaHitMode";

        [Test]
        public void Convert_narrows_an_ez_library_into_the_official_schema()
        {
            string root = RealisticEzRealmSeeder.NewRoot("convert-narrow");

            try
            {
                string ezPath = Path.Combine(root, "client.realm");
                string targetPath = Path.Combine(root, "narrowed.realm");

                seedEzLibrary(ezPath);

                OfficialSchemaSource official = readOfficialSchemaSource();

                // 过滤后应搬的行由测试自己按「官方能不能用」的朴素判据数出来：外部托管的集、其下的难度、
                // 挂在它们上面的成绩，以及 Ez 判定语义 / Ez 专用 mod 的成绩都不该过去。过滤写错（多搬或少搬）
                // 都会与这里的期望值对不上。
                ConvertExpectation expected;

                using (var ez = DynamicRealmSession.OpenDynamic(ezPath, readOnly: true))
                {
                    Assert.That(DynamicSchemaReader.Read(ez).HasClass(ez_only_class), Is.True, "Ez 源库没有 Ez 表，测试失去意义。");

                    expected = readExpectation(ez);
                }

                Assert.That(expected.SetsBefore, Is.GreaterThan(0), "Ez 源库里没有谱面集。");
                Assert.That(expected.KeptSets, Is.Not.Empty, "Ez 源库里没有被留下的谱面集，测试失去意义。");
                Assert.That(expected.DroppedSets, Is.Not.Empty, "源库里没有会被滤掉的谱面集，测试失去意义。");
                Assert.That(expected.DroppedScores, Is.Not.Empty, "源库里没有会被滤掉的成绩，测试失去意义。");

                OfficialConvertStats stats = DynamicOfficialConverter.Convert(ezPath, official, targetPath);

                RealmNativeLifetime.Flush();

                Assert.That(stats.UpstreamVersion, Is.EqualTo(official.UpstreamVersion));
                Assert.That(stats.RowsWritten, Is.GreaterThan(0), "搬运没有落任何行。");
                Assert.That(stats.DroppedClasses, Does.Contain(ez_only_class), "Ez 表没有被记为剔除。");
                Assert.That(
                    stats.DroppedColumns,
                    Does.Contain($"{OfficialBaselineSchema.BeatmapSet}.{ez_only_beatmap_set_column}"),
                    "Ez 列没有被记为剔除。");
                Assert.That(stats.Notes, Has.Some.Contains("谱面集"), "被滤掉的谱面集没有出现在报告里。");
                Assert.That(stats.Notes, Has.Some.Contains("成绩"), "被滤掉的成绩没有出现在报告里。");

                using var produced = DynamicRealmSession.OpenDynamic(targetPath, readOnly: true);
                RealmSchemaSnapshot producedSchema = DynamicSchemaReader.Read(produced);

                Assert.That(produced.DiskSchemaVersion, Is.EqualTo(official.UpstreamVersion), "产物不是官方版本号。");
                Assert.That(producedSchema.FindDifferences(official.Snapshot), Is.Empty, "产物 schema 与官方不一致。");
                Assert.That(produced.HasClass(ez_only_class), Is.False, "产物里出现了 Ez 表。");
                Assert.That(produced.HasProperty(OfficialBaselineSchema.BeatmapSet, ez_only_beatmap_set_column), Is.False, "产物里出现了谱面集的 Ez 列。");
                Assert.That(produced.HasProperty(OfficialBaselineSchema.Score, ez_only_score_column), Is.False, "产物里出现了成绩的 Ez 列。");

                Assert.That(count(produced, OfficialBaselineSchema.BeatmapSet), Is.EqualTo(expected.KeptSets.Count), "产物里的谱面集行数与官方能用的集合不符。");
                Assert.That(count(produced, OfficialBaselineSchema.Beatmap), Is.EqualTo(expected.KeptBeatmaps.Count), "产物里的难度行数与被留下的谱面集不符。");
                Assert.That(count(produced, OfficialBaselineSchema.Score), Is.EqualTo(expected.KeptScores.Count), "产物里的成绩行数与官方能还原的数量不符。");

                // 被滤掉的行必须一行都不在产物里：这是「官方端读得到、且读得对」的反面保证。
                assertIdsAbsent(produced, OfficialBaselineSchema.BeatmapSet, expected.DroppedSets);
                assertIdsAbsent(produced, OfficialBaselineSchema.Beatmap, expected.DroppedBeatmaps);
                assertIdsAbsent(produced, OfficialBaselineSchema.Score, expected.DroppedScores);

                var set = DynamicRealmAccess.Find(produced.Realm, OfficialBaselineSchema.BeatmapSet, expected.KeptSets[0]);
                Assert.That(set, Is.Not.Null, "产物里缺了源库中被留下的谱面集。");
                Assert.That(DynamicRealmAccess.GetString(set, "Hash"), Is.Not.Empty, "产物里谱面集的 Hash 丢了。");
                Assert.That(DynamicRealmAccess.EnumerateObjects(set!, "Beatmaps").Any(), Is.True, "产物里谱面集的难度列表是空的。");
            }
            finally
            {
                RealisticEzRealmSeeder.Cleanup(root);
            }
        }

        [Test]
        public void Converted_library_opens_with_the_official_dll()
        {
            if (!OfficialDllOpenCheck.WorkerAvailable)
                Assert.Ignore("Official Worker 未复制到测试输出，跳过官方 DLL 打开验收。");

            string root = RealisticEzRealmSeeder.NewRoot("convert-official-dll");

            try
            {
                string ezPath = Path.Combine(root, "client.realm");
                string targetPath = Path.Combine(root, "narrowed.realm");

                seedEzLibrary(ezPath);

                OfficialSchemaSource official = readOfficialSchemaSource();
                DynamicOfficialConverter.Convert(ezPath, official, targetPath);

                RealmNativeLifetime.Flush();

                Assert.That(
                    OfficialDllOpenCheck.TryOpen(targetPath, official.UpstreamVersion, out string? error),
                    Is.True,
                    $"产物无法被官方 DLL 打开：{error}");
            }
            finally
            {
                RealisticEzRealmSeeder.Cleanup(root);
            }
        }

        [Test]
        public async Task Service_converts_in_place_and_keeps_the_original_as_backup()
        {
            // 走产品入口：备份 → 收窄 → 原地覆盖。用户拿到的是「同一个路径、已经是官方库」，
            // 原 Ez 库只在备份里。
            string root = RealisticEzRealmSeeder.NewRoot("convert-service");

            try
            {
                string ezPath = Path.Combine(root, "client.realm");
                seedEzLibrary(ezPath);

                string officialPath = Path.Combine(root, "official-52.realm");
                createEmptyOfficialLibraryViaWorker(officialPath);

                var service = new RealmRealmDataService(new RealmFileRegistry());
                RealmFileEntry entry = await service.RegisterRealmFileAsync(ezPath);

                string backupDir = Path.Combine(root, "backups");

                RealmOfficialConversionResult result = await service.ConvertToOfficialRealmAsync(
                    entry.Id,
                    officialSchemaSourcePath: officialPath,
                    backupDirectory: backupDir);

                RealmNativeLifetime.Flush();

                Assert.That(result.TargetRealmFilePath, Is.EqualTo(Path.GetFullPath(ezPath)), "产物必须覆盖所选文件本身。");
                Assert.That(result.SourceSchemaVersion, Is.EqualTo(ez_schema));
                Assert.That(result.TargetSchemaVersion, Is.EqualTo(official_upstream));
                Assert.That(result.AppliedCount, Is.GreaterThan(0));
                Assert.That(result.BackupPath, Is.Not.Null);
                Assert.That(File.Exists(result.BackupPath), Is.True, "没有生成备份。");
                Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(result.BackupPath!), Is.EqualTo(ez_schema), "备份必须是原来的 Ez 库。");
                Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(ezPath), Is.EqualTo(official_upstream), "原路径上的文件还不是官方库。");

                using var narrowed = DynamicRealmSession.OpenDynamic(ezPath, readOnly: true);

                Assert.That(RealmSchemaSnapshotClassifier.HasEzFingerprint(DynamicSchemaReader.Read(narrowed)), Is.False, "产物里还有 Ez 表或 Ez 列。");
                Assert.That(narrowed.HasClass(ez_only_class), Is.False);
            }
            finally
            {
                RealisticEzRealmSeeder.Cleanup(root);
            }
        }

        [Test]
        public async Task Convert_without_a_backup_folder_leaves_a_timestamped_copy_beside_the_source()
        {
            // 设置里没有备份目录时也不能裸覆盖：退化为「同目录 + 文件名加时间戳后缀」。
            string root = RealisticEzRealmSeeder.NewRoot("convert-beside-backup");

            try
            {
                string ezPath = Path.Combine(root, "client.realm");
                seedEzLibrary(ezPath);

                string officialPath = Path.Combine(root, "official-52.realm");
                createEmptyOfficialLibraryViaWorker(officialPath);

                var service = new RealmRealmDataService(new RealmFileRegistry());
                RealmFileEntry entry = await service.RegisterRealmFileAsync(ezPath);

                RealmOfficialConversionResult result = await service.ConvertToOfficialRealmAsync(
                    entry.Id,
                    officialSchemaSourcePath: officialPath,
                    backupDirectory: string.Empty);

                RealmNativeLifetime.Flush();

                Assert.That(result.BackupPath, Is.Not.Null);
                Assert.That(Path.GetDirectoryName(result.BackupPath!), Is.EqualTo(Path.GetDirectoryName(Path.GetFullPath(ezPath))));
                Assert.That(Path.GetFileName(result.BackupPath!), Does.Contain("_ezbackup_"));
                Assert.That(File.Exists(result.BackupPath!), Is.True);
                Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(result.BackupPath!), Is.EqualTo(ez_schema), "同目录副本必须是原来的 Ez 库。");
                Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(ezPath), Is.EqualTo(official_upstream));
            }
            finally
            {
                RealisticEzRealmSeeder.Cleanup(root);
            }
        }

        [Test]
        public void Official_schema_source_must_match_the_upstream_of_the_ez_library()
        {
            string root = RealisticEzRealmSeeder.NewRoot("convert-source-mismatch");

            try
            {
                string officialPath = Path.Combine(root, "official-52.realm");
                createEmptyOfficialLibraryViaWorker(officialPath);

                // 用它去转一份「官方 53」的 Ez 库：版本对不上必须拒绝，而不是拿 52 的 schema 去凑。
                var mismatch = Assert.Throws<RealmUserOperationException>(
                    () => OfficialSchemaSourceResolver.FromFile(officialPath, official_upstream + 1));

                Assert.That(mismatch!.Kind, Is.EqualTo(RealmUserErrorKind.SchemaModelMismatch));
                Assert.That(mismatch.Message, Does.Contain($"官方 {official_upstream + 1}"));

                // 版本对得上时给出可用的 schema：类数与官方库本体一致。
                OfficialSchemaSource ok = OfficialSchemaSourceResolver.FromFile(officialPath, official_upstream);
                Assert.That(ok.UpstreamVersion, Is.EqualTo(official_upstream));
                Assert.That(ok.Snapshot.ClassCount, Is.GreaterThan(0));
            }
            finally
            {
                RealisticEzRealmSeeder.Cleanup(root);
            }
        }

        private static void seedEzLibrary(string path)
        {
            RealisticEzRealmSeeder.CreateCurrentEzRealm(path, ez_schema);
            RealisticEzRealmSeeder.SeedFromOfficialSample(path, ez_schema);
            RealisticEzRealmSeeder.WriteEzOnlyValues(path, ez_schema);
        }

        /// <summary>「转官方后官方端还能用的行」与「必须被滤掉的行」，以及源库总数（用于确认样本有两侧）。</summary>
        private sealed record ConvertExpectation(
            long SetsBefore,
            List<Guid> KeptSets,
            List<Guid> KeptBeatmaps,
            List<Guid> KeptScores,
            List<Guid> DroppedSets,
            List<Guid> DroppedBeatmaps,
            List<Guid> DroppedScores);

        /// <summary>
        /// 测试侧独立算一遍期望：判据刻意写死成「官方四规则集 + 非外部托管 + 非软删 + 双 Lazer 判定 + mod 全在
        /// 官方名录」，与产品侧的 <c>OfficialExportPolicy</c> 分开实现，产品漏判/多判都能被这组期望抓出来。
        /// </summary>
        private static ConvertExpectation readExpectation(DynamicRealmSession ez)
        {
            var officialRulesets = new HashSet<string>(StringComparer.Ordinal) { "osu", "mania", "taiko", "catch" };

            bool rulesetOk(IRealmObjectBase? row) => officialRulesets.Contains(DynamicRealmAccess.GetString(row, "ShortName"));

            var rulesetsByName = DynamicRealmAccess.All(ez.Realm, OfficialBaselineSchema.Ruleset)
                                                   .AsEnumerable()
                                                   .ToDictionary(r => DynamicRealmAccess.GetString(r, "ShortName"), r => rulesetOk(r));

            var beatmaps = DynamicRealmAccess.All(ez.Realm, OfficialBaselineSchema.Beatmap)
                                             .AsEnumerable()
                                             .Select(b => new
                                             {
                                                 Id = DynamicRealmAccess.Get<Guid>(b, "ID"),
                                                 Hash = DynamicRealmAccess.GetString(b, "Hash"),
                                                 Hidden = DynamicRealmAccess.Get<bool>(b, "Hidden"),
                                                 RulesetOk = rulesetsByName.GetValueOrDefault(
                                                     DynamicRealmAccess.GetString(DynamicRealmAccess.Get<IRealmObjectBase>(b, "Ruleset"), "ShortName")),
                                                 SetId = DynamicRealmAccess.Get<IRealmObjectBase>(b, "BeatmapSet") is { } parent
                                                     ? DynamicRealmAccess.Get<Guid>(parent, "ID")
                                                     : Guid.Empty,
                                             })
                                             .ToList();

            var keptSets = new List<Guid>();
            var droppedSets = new List<Guid>();

            foreach (IRealmObjectBase set in DynamicRealmAccess.All(ez.Realm, OfficialBaselineSchema.BeatmapSet).AsEnumerable())
            {
                Guid id = DynamicRealmAccess.Get<Guid>(set, "ID");
                bool external = DynamicRealmAccess.Get<int>(set, "HostingKind") == 1;
                bool deletePending = DynamicRealmAccess.Get<bool>(set, "DeletePending");
                bool hasUsableBeatmap = beatmaps.Any(b => b.SetId == id && !b.Hidden && b.RulesetOk);

                (external || deletePending || !hasUsableBeatmap ? droppedSets : keptSets).Add(id);
            }

            var keptBeatmaps = beatmaps.Where(b => !b.Hidden && b.RulesetOk && keptSets.Contains(b.SetId)).ToList();
            var keptHashes = keptBeatmaps.Select(b => b.Hash).ToHashSet(StringComparer.Ordinal);

            var keptScores = new List<Guid>();
            var droppedScores = new List<Guid>();

            foreach (IRealmObjectBase score in DynamicRealmAccess.All(ez.Realm, OfficialBaselineSchema.Score).AsEnumerable())
            {
                bool ezSemantics = DynamicRealmAccess.Get<int>(score, "ManiaHitMode") > 0 || DynamicRealmAccess.Get<int>(score, "ManiaHealthMode") > 0;
                bool modsOk = !DynamicRealmAccess.GetString(score, "Mods").Contains("\"NCl\"", StringComparison.Ordinal);
                bool beatmapKept = keptHashes.Contains(DynamicRealmAccess.GetString(score, "BeatmapHash"));

                bool keep = rulesetOk(DynamicRealmAccess.Get<IRealmObjectBase>(score, "Ruleset"))
                            && !ezSemantics
                            && modsOk
                            && !DynamicRealmAccess.Get<bool>(score, "DeletePending")
                            && beatmapKept;

                (keep ? keptScores : droppedScores).Add(DynamicRealmAccess.Get<Guid>(score, "ID"));
            }

            return new ConvertExpectation(
                count(ez, OfficialBaselineSchema.BeatmapSet),
                keptSets,
                keptBeatmaps.Select(b => b.Id).ToList(),
                keptScores,
                droppedSets,
                beatmaps.Where(b => !keptBeatmaps.Contains(b)).Select(b => b.Id).ToList(),
                droppedScores);
        }

        private static void assertIdsAbsent(DynamicRealmSession produced, string className, IReadOnlyList<Guid> ids)
        {
            foreach (Guid id in ids)
                Assert.That(DynamicRealmAccess.Find(produced.Realm, className, id), Is.Null, $"{className} {id} 本应被滤掉，却出现在了产物里。");
        }

        /// <summary>
        /// 官方 52 的 schema 事实来源：让 OfficialWrite Worker 用官方镜像 schema 落一份空库，
        /// 再从它采集快照——与本工具在真实场景里「指向一份官方库」的路径完全一致。
        /// </summary>
        private static OfficialSchemaSource readOfficialSchemaSource()
        {
            string worker = OfficialWorkerProcess.ResolveWorkerExecutablePathForTests();

            if (!File.Exists(worker))
                Assert.Ignore($"OfficialWrite Worker 未复制到测试输出，无法造官方 {official_upstream} 参考库：{worker}");

            string root = Path.Combine(Path.GetTempPath(), "EzRealmSyncOfficialSchema", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                string officialPath = Path.Combine(root, "official.realm");
                createEmptyOfficialLibraryViaWorker(officialPath);

                return OfficialSchemaSourceResolver.FromFile(officialPath, official_upstream);
            }
            finally
            {
                try
                {
                    Directory.Delete(root, recursive: true);
                }
                catch
                {
                    // 清理失败不影响断言结果。
                }
            }
        }

        private static void createEmptyOfficialLibraryViaWorker(string path)
        {
            OfficialWorkerProcess.Run(new OfficialConvertJob
            {
                TargetUpstreamSchema = official_upstream,
                TargetRealmPath = path,
                Rulesets = [new OfficialRulesetDto { ShortName = "osu", OnlineID = 0, Name = "osu!" }],
            });

            Assert.That(RealmDiskSchemaReader.TryReadSchemaVersion(path), Is.EqualTo(official_upstream), "参考官方库没落成。");
        }

        private static long count(DynamicRealmSession session, string className) =>
            DynamicRealmAccess.All(session.Realm, className).AsEnumerable().LongCount();
    }
}
