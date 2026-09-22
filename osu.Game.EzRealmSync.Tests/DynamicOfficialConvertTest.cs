using System.Linq;
using NUnit.Framework;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Realm.Dynamic;
using osu.Game.EzRealmSync.Tests.TestInfrastructure;

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

                long setsBefore;
                long scoresBefore;
                Guid firstSetId;

                using (var ez = DynamicRealmSession.OpenDynamic(ezPath, readOnly: true))
                {
                    Assert.That(DynamicSchemaReader.Read(ez).HasClass(ez_only_class), Is.True, "Ez 源库没有 Ez 表，测试失去意义。");

                    setsBefore = count(ez, OfficialBaselineSchema.BeatmapSet);
                    scoresBefore = count(ez, OfficialBaselineSchema.Score);
                    firstSetId = DynamicRealmAccess.All(ez.Realm, OfficialBaselineSchema.BeatmapSet)
                                                     .AsEnumerable()
                                                     .Select(s => DynamicRealmAccess.Get<Guid>(s, "ID"))
                                                     .FirstOrDefault();
                }

                Assert.That(setsBefore, Is.GreaterThan(0), "Ez 源库里没有谱面集。");

                OfficialConvertStats stats = DynamicOfficialConverter.Convert(ezPath, official, targetPath);

                RealmNativeLifetime.Flush();

                Assert.That(stats.UpstreamVersion, Is.EqualTo(official.UpstreamVersion));
                Assert.That(stats.RowsWritten, Is.GreaterThan(0), "搬运没有落任何行。");
                Assert.That(stats.DroppedClasses, Does.Contain(ez_only_class), "Ez 表没有被记为剔除。");
                Assert.That(
                    stats.DroppedColumns,
                    Does.Contain($"{OfficialBaselineSchema.BeatmapSet}.{ez_only_beatmap_set_column}"),
                    "Ez 列没有被记为剔除。");

                using var produced = DynamicRealmSession.OpenDynamic(targetPath, readOnly: true);
                RealmSchemaSnapshot producedSchema = DynamicSchemaReader.Read(produced);

                Assert.That(produced.DiskSchemaVersion, Is.EqualTo(official.UpstreamVersion), "产物不是官方版本号。");
                Assert.That(producedSchema.FindDifferences(official.Snapshot), Is.Empty, "产物 schema 与官方不一致。");
                Assert.That(produced.HasClass(ez_only_class), Is.False, "产物里出现了 Ez 表。");
                Assert.That(produced.HasProperty(OfficialBaselineSchema.BeatmapSet, ez_only_beatmap_set_column), Is.False, "产物里出现了谱面集的 Ez 列。");
                Assert.That(produced.HasProperty(OfficialBaselineSchema.Score, ez_only_score_column), Is.False, "产物里出现了成绩的 Ez 列。");

                Assert.That(count(produced, OfficialBaselineSchema.BeatmapSet), Is.EqualTo(setsBefore), "谱面集行数在产物里对不上。");
                Assert.That(count(produced, OfficialBaselineSchema.Score), Is.EqualTo(scoresBefore), "成绩行数在产物里对不上。");

                var set = DynamicRealmAccess.Find(produced.Realm, OfficialBaselineSchema.BeatmapSet, firstSetId);
                Assert.That(set, Is.Not.Null, "产物里缺了源库的谱面集。");
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
