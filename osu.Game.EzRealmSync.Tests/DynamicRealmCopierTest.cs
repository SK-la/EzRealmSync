using System.Linq;
using NUnit.Framework;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Realm.Dynamic;
using osu.Game.EzRealmSync.Tests.TestInfrastructure;
using Realms;

namespace osu.Game.EzRealmSync.Tests
{
    /// <summary>
    /// 「按快照还原 schema」+「跨库逐格搬运」的对照测试。
    ///
    /// 这两件事是转官方与建官方库的底座：目标库必须<b>恰好</b>是官方 schema（不多一列 Ez、不少一列官方），
    /// 且源库公共列上的数据一格不少。测试用真实样本，不用手搭对象——链接、嵌入、文件用法这些结构
    /// 手搭最容易漏，漏了对照也跟着漏。
    /// </summary>
    [TestFixture]
    public class DynamicRealmCopierTest
    {
        private const string ez_only_class = "EzDanEstimate";
        private const string ez_only_beatmap_set_column = "ExternalContentRoot";
        private const string ez_only_score_column = "ManiaHitMode";

        [Test]
        public void Dynamic_rows_keep_a_stable_identity_across_reads()
        {
            // 同一行两次读出来是**不同包装实例**（已实测），所以跨库搬运的「源行 → 目标行」映射
            // 不能按引用记。这里钉住替代前提：包装对象的 Equals/GetHashCode 按行身份比较，
            // 有主键的类靠主键，没有主键的类（BeatmapMetadata）也能区分不同行。
            RealmSampleInfo sample = requireOfficialSample();

            using var session = RealmOpen(officialSamplePath(sample));

            var sets = DynamicRealmAccess.All(session.Realm, OfficialBaselineSchema.BeatmapSet).AsEnumerable().ToList();
            var setsAgain = DynamicRealmAccess.All(session.Realm, OfficialBaselineSchema.BeatmapSet).AsEnumerable().ToList();

            Assert.That(sets, Is.Not.Empty, "官方样本里没有谱面集。");
            Assert.That(sets.Count, Is.EqualTo(setsAgain.Count));

            var identity = new Dictionary<IRealmObjectBase, IRealmObjectBase>();

            foreach (IRealmObjectBase set in sets)
                identity[set] = set;

            foreach (IRealmObjectBase set in setsAgain)
                Assert.That(identity.ContainsKey(set), Is.True, "同一行两次读出后无法在字典里对上（有主键的类）。");

            // 有主键的类：从链接走到的行必须与整表枚举出来的行互相 equal。
            IRealmObjectBase? linkedBeatmap = sets.SelectMany(s => DynamicRealmAccess.EnumerateObjects(s, "Beatmaps")).FirstOrDefault();
            Assert.That(linkedBeatmap, Is.Not.Null, "官方样本的谱面集没有难度。");

            Guid beatmapId = DynamicRealmAccess.Get<Guid>(linkedBeatmap!, "ID");
            IRealmObjectBase? enumerated = DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.Beatmap, beatmapId);
            Assert.That(enumerated, Is.Not.Null);
            Assert.That(linkedBeatmap!.Equals(enumerated), Is.True, "从链接取到的行与按主键取到的行不是同一行。");

            // 无主键的类：BeatmapMetadata 在两条难度间共享，必须能靠 Equals 认出「同一行」。
            IRealmObjectBase? metadata = DynamicRealmAccess.Get<IRealmObjectBase>(linkedBeatmap!, "Metadata");
            Assert.That(metadata, Is.Not.Null, "难度没有元数据行。");

            var metadataRows = DynamicRealmAccess.All(session.Realm, "BeatmapMetadata").AsEnumerable().ToList();
            Assert.That(metadataRows.Count(m => m.Equals(metadata)), Is.EqualTo(1), "无主键的行无法唯一识别（共享的元数据会被当成多行）。");
        }

        [Test]
        public void Schema_rebuilt_from_a_snapshot_matches_the_source_library()
        {
            RealmSampleInfo sample = requireOfficialSample();
            string root = newRoot("schema-rebuild");

            try
            {
                using var source = RealmOpen(officialSamplePath(sample));
                RealmSchemaSnapshot snapshot = DynamicSchemaReader.Read(source);
                int version = source.DiskSchemaVersion;

                string builtPath = Path.Combine(root, "built.realm");

                using (DynamicRealmSession.OpenDynamicWithSchema(builtPath, RealmSchemaDefinitionFactory.Create(snapshot), (ulong)version, readOnly: false))
                {
                }

                RealmNativeLifetime.Flush();

                using var rebuilt = RealmOpen(builtPath);
                Assert.That(rebuilt.DiskSchemaVersion, Is.EqualTo(version), "新建库的磁盘 schema 版本与来源不一致。");
                Assert.That(DynamicSchemaReader.Read(rebuilt).FindDifferences(snapshot), Is.Empty, "按快照还原出来的 schema 与来源不一致。");
            }
            finally
            {
                cleanup(root);
            }
        }

        [Test]
        public void Copy_into_the_same_schema_preserves_every_cell()
        {
            // 目标 schema == 源 schema 时，搬运必须是无损的：两份整表导出应当逐格一致。
            // 这条同时覆盖了链接（Beatmap.BeatmapSet）、嵌入（Difficulty / Files）、值列表（Pauses）。
            RealmSampleInfo sample = requireOfficialSample();
            string root = newRoot("same-schema");

            try
            {
                string targetPath = Path.Combine(root, "copy.realm");
                DynamicRealmDump sourceDump;

                using (var source = RealmOpen(officialSamplePath(sample)))
                using (var target = DynamicRealmSession.OpenDynamicWithSchema(
                           targetPath,
                           RealmSchemaDefinitionFactory.Create(DynamicSchemaReader.Read(source)),
                           (ulong)source.DiskSchemaVersion,
                           readOnly: false))
                {
                    DynamicRealmCopier.Copy(source, target);
                }

                RealmNativeLifetime.Flush();

                // 每类限 25 行：行数照常精确比对，单元格逐格比对只在前 25 行上做。
                // 不限行时整库导出会退化成逐行回查链接，真实样本上要跑几十分钟（不是失败，是量级问题）。
                var dumpOptions = new DynamicDumpOptions { MaxRowsPerClass = 25 };

                using (var source = RealmOpen(officialSamplePath(sample)))
                    sourceDump = DynamicRealmDumper.Dump(source, dumpOptions);

                using (var target = RealmOpen(targetPath))
                {
                    var targetDump = DynamicRealmDumper.Dump(target, dumpOptions);

                    Assert.That(targetDump.FindDifferences(sourceDump), Is.Empty, "整库搬到同 schema 的新库后与源库不一致。");
                    Assert.That(targetDump.Classes.Where(c => !c.IsEmbedded).Sum(c => c.RowCount), Is.GreaterThan(0), "目标库一行都没有，对照没有意义。");
                }
            }
            finally
            {
                cleanup(root);
            }
        }

        [Test]
        public void Copy_drops_ez_tables_and_columns_when_the_target_is_official()
        {
            // 真实感 Ez 库（含 Ez 表 / Ez 列 + 真实对象）→ 官方 schema 的新库：
            //   · 目标必须恰好是官方 schema（Ez 表与 Ez 列都不许带过去）
            //   · 公共类行数一致、公共列数据保留
            string root = RealisticEzRealmSeeder.NewRoot("copy-to-official");

            try
            {
                RealmSampleInfo official = requireOfficialSample();
                string ezPath = Path.Combine(root, "ez.realm");
                string targetPath = Path.Combine(root, "official.realm");

                RealisticEzRealmSeeder.CreateCurrentEzRealm(ezPath, EzSchema);
                RealisticEzRealmSeeder.SeedFromOfficialSample(ezPath, EzSchema);
                RealisticEzRealmSeeder.WriteEzOnlyValues(ezPath, EzSchema);

                RealmSchemaSnapshot officialSchema;
                int officialVersion;

                using (var source = RealmOpen(official.RealmFilePath))
                {
                    officialSchema = DynamicSchemaReader.Read(source);
                    officialVersion = source.DiskSchemaVersion;
                }

                var counts = new Dictionary<string, long>(StringComparer.Ordinal);
                var firstSetId = Guid.Empty;

                using (var target = DynamicRealmSession.OpenDynamicWithSchema(
                           targetPath,
                           RealmSchemaDefinitionFactory.Create(officialSchema),
                           (ulong)officialVersion,
                           readOnly: false))
                using (var source = RealmOpen(ezPath))
                {
                    Assert.That(DynamicSchemaReader.Read(source).HasClass(ez_only_class), Is.True, "Ez 源库没有 Ez 表，测试失去意义。");

                    DynamicCopyResult result = DynamicRealmCopier.Copy(source, target);
                    Assert.That(result.Rows, Is.GreaterThan(0), "搬运没有落任何行。");

                    foreach (RealmClassSchema classSchema in officialSchema.Classes.Where(c => !c.IsEmbedded))
                        counts[classSchema.Name] = DynamicRealmAccess.All(source.Realm, classSchema.Name).AsEnumerable().LongCount();

                    firstSetId = DynamicRealmAccess.All(source.Realm, OfficialBaselineSchema.BeatmapSet)
                                                  .AsEnumerable()
                                                  .Select(s => DynamicRealmAccess.Get<Guid>(s, "ID"))
                                                  .FirstOrDefault();
                }

                RealmNativeLifetime.Flush();

                using var copied = RealmOpen(targetPath);
                RealmSchemaSnapshot copiedSchema = DynamicSchemaReader.Read(copied);

                Assert.That(copied.DiskSchemaVersion, Is.EqualTo(officialVersion), "产物不是官方 schema 版本。");
                Assert.That(copiedSchema.FindDifferences(officialSchema), Is.Empty, "产物 schema 与官方 schema 不一致（Ez 表或 Ez 列被带了过去）。");

                Assert.That(copied.HasClass(ez_only_class), Is.False, "产物里出现了 Ez 表。");
                Assert.That(copied.HasProperty(OfficialBaselineSchema.BeatmapSet, ez_only_beatmap_set_column), Is.False, "产物里出现了谱面集的 Ez 列。");
                Assert.That(copied.HasProperty(OfficialBaselineSchema.Score, ez_only_score_column), Is.False, "产物里出现了成绩的 Ez 列。");

                foreach ((string className, long expected) in counts)
                {
                    long actual = DynamicRealmAccess.All(copied.Realm, className).AsEnumerable().LongCount();
                    Assert.That(actual, Is.EqualTo(expected), $"{className} 的行数在产物里对不上。");
                }

                Assert.That(firstSetId, Is.Not.EqualTo(Guid.Empty), "Ez 源库里没有谱面集。");

                var sourceSet = DynamicRealmAccess.Find(copied.Realm, OfficialBaselineSchema.BeatmapSet, firstSetId);
                Assert.That(sourceSet, Is.Not.Null, "产物里缺了源库的谱面集。");

                string? hash = DynamicRealmAccess.GetString(sourceSet, "Hash");
                Assert.That(hash, Is.Not.Empty, "产物里谱面集的 Hash 丢了。");
                Assert.That(DynamicRealmAccess.EnumerateObjects(sourceSet!, "Beatmaps").Any(), Is.True, "产物里谱面集的难度列表是空的。");
            }
            finally
            {
                RealisticEzRealmSeeder.Cleanup(root);
            }
        }

#if HAS_EZ_OSU_GAME
        private const int EzSchema = 52_010;

        [Test]
        public void Copied_official_schema_library_opens_with_the_official_dll()
        {
            // 产物要能被官方 DLL 以 pinned disk schema 打开——这是「转官方」的验收口径。
            if (!OfficialDllOpenCheck.WorkerAvailable)
                Assert.Ignore("Official Worker 未复制到测试输出，跳过官方 DLL 打开验收。");

            RealmSampleInfo official = requireOfficialSample();
            string root = newRoot("official-dll");

            try
            {
                string targetPath = Path.Combine(root, "official.realm");
                int version;

                using (var source = RealmOpen(official.RealmFilePath))
                using (var target = DynamicRealmSession.OpenDynamicWithSchema(
                           targetPath,
                           RealmSchemaDefinitionFactory.Create(DynamicSchemaReader.Read(source)),
                           (ulong)source.DiskSchemaVersion,
                           readOnly: false))
                {
                    version = source.DiskSchemaVersion;
                    DynamicRealmCopier.Copy(source, target);
                }

                RealmNativeLifetime.Flush();

                Assert.That(
                    OfficialDllOpenCheck.TryOpen(targetPath, version, out string? error),
                    Is.True,
                    $"产物无法被官方 DLL 打开：{error}");
            }
            finally
            {
                cleanup(root);
            }
        }
#endif

        private static string officialSamplePath(RealmSampleInfo sample) => sample.RealmFilePath;

        private static RealmSampleInfo requireOfficialSample()
        {
            RealmSampleInfo? sample = RealmSampleFixture.GetAllSamples()
                                                        .FirstOrDefault(s => s.RealmFileExists && s.DiskSchemaKind == nameof(RealmDiskSchemaKind.PpyClient));

            if (sample == null)
                Assert.Ignore("未放置官方样本，跳过搬运对照。");

            return sample;
        }

        private static DynamicRealmSession RealmOpen(string path) => DynamicRealmSession.OpenDynamic(path, readOnly: true);

        private static string newRoot(string name)
        {
            string root = Path.Combine(Path.GetTempPath(), "EzRealmSyncCopier", name + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void cleanup(string root)
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
    }
}
