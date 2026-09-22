#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Realm.Dynamic;
using osu.Game.EzRealmSync.Tests.TestInfrastructure;
using osu.Game.Scoring;
using osu.Game.Skinning;

namespace osu.Game.EzRealmSync.Tests
{
    /// <summary>
    /// 动态读写引擎的正确性硬防线：同一份库，typed（<c>osu.Game</c> 模型）与 dynamic（不传 schema）
    /// 各导出**整表整列**，逐单元格比对；同一批改动分别用两种方式写入两份副本，再比对结果。
    ///
    /// 动态写没有编译期检查，列名写错、类型收窄错、列表/链接漏写都不会报错——只有这类对照能抓到。
    /// 因此这组测试在 typed 路径从产品里退休之后仍要保留（typed 侧由测试工程自己的 DLL 提供）。
    ///
    /// 覆盖不到的地方也显式记账：模型缺类 / 缺列会断言出来（见 <see cref="TypedDumpResult"/>），
    /// 否则 parity 会静默少比一整张表。
    /// </summary>
    [TestFixture]
    public class DynamicDumpParityTest
    {
        private const int official_schema = 51;

        [Test]
        public void Typed_and_dynamic_dumps_agree_on_a_realistic_ez_realm()
        {
            string root = newRoot("ez-parity");
            int schema = RealmAccess.EzFileSchemaVersion;

            try
            {
                string targetPath = Path.Combine(root, "target.realm");
                createCurrentEzRealm(targetPath, schema);
                seedFromOfficialSample(targetPath, schema);
                writeEzOnlyValues(targetPath, schema);

                var (typed, dynamic) = dumpBothWays(targetPath, schema);

                assertModelCoversDisk(typed);

                Assert.That(dynamic.FindDifferences(typed.Dump), Is.Empty,
                    "typed 与 dynamic 读同一份库的结果不一致（上面列出第一处差异）。");
            }
            finally
            {
                cleanup(root);
            }
        }

        [Test]
        public void Same_change_written_typed_and_dynamically_produces_identical_dumps()
        {
            string root = newRoot("write-parity");
            int schema = RealmAccess.EzFileSchemaVersion;

            try
            {
                string seedPath = Path.Combine(root, "seed.realm");
                createCurrentEzRealm(seedPath, schema);
                seedFromOfficialSample(seedPath, schema);

                string typedPath = Path.Combine(root, "typed.realm");
                string dynamicPath = Path.Combine(root, "dynamic.realm");
                File.Copy(seedPath, typedPath);
                File.Copy(seedPath, dynamicPath);
                RealmNativeLifetime.Flush();

                var change = pickChangeTargets(seedPath, schema);

                writeChangeTyped(typedPath, schema, change);
                writeChangeDynamic(dynamicPath, change);

                DynamicRealmDump typedDump = dumpDynamic(typedPath);
                DynamicRealmDump dynamicDump = dumpDynamic(dynamicPath);

                Assert.That(typedDump.FindDifferences(dynamicDump), Is.Empty,
                    "同一批改动分别用 typed 与 dynamic 写入后结果不同。");

                // 反向自检：改动确实落到了两份库里，否则「一致」可能只是两边都没写进去。
                assertChangeApplied(typedDump, change);
            }
            finally
            {
                cleanup(root);
            }
        }

        [Test]
        public void Real_samples_dump_identically_twice_and_cover_every_class()
        {
            // 真实样本几万行，且链接 / 反向链接列要逐行回查对方表，整库导出是 O(类数 × 行数) 起步的活。
            // 逐类限行正是为这份样本准备的：本测试要证明的是「导出可复现 + 每类每列都读得出来」，
            // 与读多少行无关，而限行把这份测试从分钟级压到秒级。
            var options = new DynamicDumpOptions { MaxRowsPerClass = sample_row_limit };

            foreach (RealmSampleInfo sample in RealmSampleFixture.GetAllSamples().Where(s => s.RealmFileExists))
            {
                using var copy = RealmSampleFixture.CreateWritableCopy(sample);

                DynamicRealmDump first = dumpDynamic(copy.RealmFilePath, options);
                DynamicRealmDump second = dumpDynamic(copy.RealmFilePath, options);

                Assert.Multiple(() =>
                {
                    Assert.That(first.FindDifferences(second), Is.Empty, $"{sample.Kind}：同一份库两次导出结果不同。");
                    Assert.That(first.Classes, Is.Not.Empty, $"{sample.Kind}：没有导出任何类。");

                    foreach (DynamicDumpClass dumped in first.Classes)
                    {
                        // 允许两种note：嵌入类本来就没有独立行；非嵌入类只可能因为限行而截断。
                        string? expectedNote = dumped.IsEmbedded
                            ? "嵌入类不直接枚举"
                            : dumped.RowCount > sample_row_limit ? $"只导出了前 {sample_row_limit} 行（共 {dumped.RowCount} 行）" : null;

                        Assert.That(dumped.Note, Is.EqualTo(expectedNote),
                            $"{sample.Kind}.{dumped.Name}：导出有异常（{dumped.Note}）。");
                        Assert.That(
                            dumped.Rows.SelectMany(r => r.Cells.Values),
                            Has.None.EqualTo(DynamicDumpValue.Unreadable),
                            $"{sample.Kind}.{dumped.Name}：有列读不出来。");

                        // 列清单必须来自磁盘 schema：少一列就意味着一整列没人对照过。
                        Assert.That(dumped.Columns, Is.Not.Empty, $"{sample.Kind}.{dumped.Name}：该类没有列。");
                    }
                });

                RealmNativeLifetime.Flush();
            }
        }

        private static (TypedDumpResult Typed, DynamicRealmDump Dynamic) dumpBothWays(string realmPath, int schema)
        {
            DynamicRealmDump dynamicDump = dumpDynamic(realmPath);

            RealmSchemaSnapshot schemaSnapshot;

            using (var session = openDynamic(realmPath))
                schemaSnapshot = DynamicSchemaReader.Read(session.Realm);

            using var access = TypedRealmAccess.OpenForMutation(realmPath, schema);
            TypedDumpResult typed = null!;

            access.Run(realm => typed = TypedRealmDumper.Dump(realm, schemaSnapshot, schema));

            return (typed, dynamicDump);
        }

        private static DynamicRealmDump dumpDynamic(string realmPath, DynamicDumpOptions? options = null)
        {
            using var session = openDynamic(realmPath);
            return DynamicRealmDumper.Dump(session, options);
        }

        private static DynamicRealmSession openDynamic(string realmPath) => DynamicRealmSession.OpenDynamic(realmPath, readOnly: true);

        private static void assertModelCoversDisk(TypedDumpResult typed)
        {
            Assert.Multiple(() =>
            {
                Assert.That(typed.ClassesWithoutModel, Is.Empty, "磁盘上有模型里没有的类，parity 会漏掉整张表。");
                Assert.That(typed.ColumnsWithoutModel, Is.Empty, "磁盘上有模型里没有的列，parity 会漏掉整列。");
            });
        }

        /// <summary>用 bundled Ez 模型落一份当前 schema 的空库；这是「当前 Ez 客户端」写出的等价产物。</summary>
        private static void createCurrentEzRealm(string path, int schema) => RealisticEzRealmSeeder.CreateCurrentEzRealm(path, schema);

        private static void seedFromOfficialSample(string targetPath, int targetSchema) => RealisticEzRealmSeeder.SeedFromOfficialSample(targetPath, targetSchema);

        private static void writeEzOnlyValues(string path, int schema) => RealisticEzRealmSeeder.WriteEzOnlyValues(path, schema);

        private static ChangeTargets pickChangeTargets(string realmPath, int schema)
        {
            using var session = openDynamic(realmPath);
            RealmSchemaSnapshot snapshot = DynamicSchemaReader.Read(session);

            Guid? set = firstId(session, snapshot, OfficialBaselineSchema.BeatmapSet);
            Guid? collection = firstId(session, snapshot, OfficialBaselineSchema.BeatmapCollection);
            Guid? skin = firstId(session, snapshot, OfficialBaselineSchema.Skin);

            Assert.That(set, Is.Not.Null, "种子库里没有谱面集，无法构造改动。");

            return new ChangeTargets(set!.Value, collection, skin);
        }

        /// <summary>取该类第一行的主键。走 <see cref="DynamicValueCodec"/> 而不是 <c>DynamicRealmAccess.Get&lt;Guid&gt;</c>：后者读不出来时静默返回默认值，会把「没找到」和 Guid.Empty 混起来。</summary>
        private static Guid? firstId(DynamicRealmSession session, RealmSchemaSnapshot snapshot, string className)
        {
            if (!snapshot.TryFindClass(className, out RealmClassSchema? classSchema) || classSchema.PrimaryKeyProperty is not { } primaryKey)
                return null;

            if (!classSchema.TryFindProperty(primaryKey, out RealmPropertySchema? primaryKeySchema))
                return null;

            foreach (var row in DynamicRealmAccess.All(session.Realm, className))
            {
                return DynamicValueCodec.Read(row, primaryKeySchema) switch
                {
                    Guid guid => guid,
                    string text when Guid.TryParse(text, out Guid parsed) => parsed,
                    _ => null,
                };
            }

            return null;
        }

        private static void writeChangeTyped(string path, int schema, ChangeTargets change)
        {
            using var access = TypedRealmAccess.OpenForMutation(path, schema);

            // 断言不能留在写事务里：抛出会让事务半途结束，失败原因反而被事务清理掩盖。
            var found = new ChangeTargets(Guid.Empty, null, null);

            access.Write(realm =>
            {
                var set = realm.All<BeatmapSetInfo>().AsEnumerable().SingleOrDefault(s => s.ID == change.SetId);
                var collection = change.CollectionId is { } collectionId
                    ? realm.All<BeatmapCollection>().AsEnumerable().SingleOrDefault(c => c.ID == collectionId)
                    : null;
                var skin = change.SkinId is { } skinId
                    ? realm.All<SkinInfo>().AsEnumerable().SingleOrDefault(s => s.ID == skinId)
                    : null;

                found = new ChangeTargets(set?.ID ?? Guid.Empty, collection?.ID, skin?.ID);

                if (set == null)
                    return;

                set.Hash = changed_set_hash;
                set.StatusInt = (int)BeatmapOnlineStatus.Ranked;
                set.DeletePending = true;

                foreach (var beatmap in set.Beatmaps)
                {
                    beatmap.DifficultyName = changed_difficulty_name;
                    beatmap.StarRating = changed_star_rating;
                    beatmap.TotalObjectCount = 1234;
                }

                if (collection != null)
                {
                    collection.Name = changed_collection_name;

                    // 集合列也纳入对照：typed 侧的 IList 与动态侧的列表写入必须落到同一结果。
                    collection.BeatmapMD5Hashes.Clear();

                    foreach (string md5 in changed_collection_hashes)
                        collection.BeatmapMD5Hashes.Add(md5);
                }

                if (skin != null)
                    skin.Name = changed_skin_name;
            });

            Assert.Multiple(() =>
            {
                Assert.That(found.SetId, Is.EqualTo(change.SetId), "typed 侧找不到要改的谱面集。");

                if (change.CollectionId != null)
                    Assert.That(found.CollectionId, Is.EqualTo(change.CollectionId), "typed 侧找不到要改的收藏夹。");

                if (change.SkinId != null)
                    Assert.That(found.SkinId, Is.EqualTo(change.SkinId), "typed 侧找不到要改的皮肤。");
            });

            RealmNativeLifetime.Flush();
        }

        private static void writeChangeDynamic(string path, ChangeTargets change)
        {
            using var session = DynamicRealmSession.OpenDynamic(path, readOnly: false);
            RealmSchemaSnapshot schema = DynamicSchemaReader.Read(session);

            using (var transaction = session.Realm.BeginWrite())
            {
                var set = DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.BeatmapSet, change.SetId);
                Assert.That(set, Is.Not.Null, "动态侧找不到要改的谱面集。");

                setProperty(session, schema, OfficialBaselineSchema.BeatmapSet, set!, "Hash", changed_set_hash);
                setProperty(session, schema, OfficialBaselineSchema.BeatmapSet, set!, "Status", (long)(int)BeatmapOnlineStatus.Ranked);
                setProperty(session, schema, OfficialBaselineSchema.BeatmapSet, set!, "DeletePending", true);

                foreach (var beatmap in DynamicRealmAccess.EnumerateObjects(set!, "Beatmaps"))
                {
                    setProperty(session, schema, OfficialBaselineSchema.Beatmap, beatmap, "DifficultyName", changed_difficulty_name);
                    setProperty(session, schema, OfficialBaselineSchema.Beatmap, beatmap, "StarRating", changed_star_rating);
                    setProperty(session, schema, OfficialBaselineSchema.Beatmap, beatmap, "TotalObjectCount", 1234L);
                }

                if (change.CollectionId is { } collectionId)
                {
                    var collection = DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.BeatmapCollection, collectionId);
                    Assert.That(collection, Is.Not.Null, "动态侧找不到要改的收藏夹。");
                    setProperty(session, schema, OfficialBaselineSchema.BeatmapCollection, collection!, "Name", changed_collection_name);
                    setProperty(session, schema, OfficialBaselineSchema.BeatmapCollection, collection!, "BeatmapMD5Hashes", changed_collection_hashes);
                }

                if (change.SkinId is { } skinId)
                {
                    var skin = DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.Skin, skinId);
                    Assert.That(skin, Is.Not.Null, "动态侧找不到要改的皮肤。");
                    setProperty(session, schema, OfficialBaselineSchema.Skin, skin!, "Name", changed_skin_name);
                }

                transaction.Commit();
            }

            RealmNativeLifetime.Flush();
        }

        private static void setProperty(
            DynamicRealmSession session,
            RealmSchemaSnapshot schema,
            string className,
            Realms.IRealmObjectBase row,
            string property,
            object value)
        {
            Assert.That(schema.TryFindClass(className, out RealmClassSchema? classSchema), Is.True, $"{className} 不在 schema 里。");
            Assert.That(classSchema!.TryFindProperty(property, out RealmPropertySchema? propertySchema), Is.True, $"{className}.{property} 不在 schema 里。");

            DynamicValueCodec.Write(row, propertySchema!, value);
        }

        private static void assertChangeApplied(DynamicRealmDump dump, ChangeTargets change)
        {
            DynamicDumpRow? setRow = dump.Find(OfficialBaselineSchema.BeatmapSet)?.Rows.FirstOrDefault(r => r.Key == change.SetId.ToString("D"));

            Assert.That(setRow, Is.Not.Null, "改动后的谱面集行不见了。");

            Assert.Multiple(() =>
            {
                Assert.That(setRow!.Cells["Hash"], Is.EqualTo(changed_set_hash), "typed 侧的改动没有落到 Hash。");
                Assert.That(setRow.Cells["DeletePending"], Is.EqualTo("true"), "typed 侧的改动没有落到 DeletePending。");

                if (change.CollectionId is { } collectionId)
                {
                    DynamicDumpRow? collectionRow = dump.Find(OfficialBaselineSchema.BeatmapCollection)?.Rows
                                                        .FirstOrDefault(r => r.Key == collectionId.ToString("D"));

                    Assert.That(collectionRow, Is.Not.Null, "改动后的收藏夹行不见了。");
                    Assert.That(collectionRow!.Cells["BeatmapMD5Hashes"],
                        Is.EqualTo($"[{string.Join(",", changed_collection_hashes)}]"), "typed 侧的列表改动没有落盘。");
                }
            });
        }

        private static string newRoot(string name)
        {
            string root = Path.Combine(Path.GetTempPath(), "EzRealmSyncDumpParity", name + "-" + Guid.NewGuid().ToString("N"));
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

        private const string changed_set_hash = "changed-set-hash";
        private const string changed_difficulty_name = "Changed";
        private const double changed_star_rating = 5.75;
        private const string changed_collection_name = "Changed Collection";
        private const string changed_skin_name = "Changed Skin";

        /// <summary>真实样本逐类读多少行。足够覆盖「每列都读得出来」，又不至于让链接回查把测试拖成分钟级。</summary>
        private const int sample_row_limit = 25;

        private static readonly string[] changed_collection_hashes = ["parity-md5-a", "parity-md5-b"];

        private readonly record struct ChangeTargets(Guid SetId, Guid? CollectionId, Guid? SkinId);
    }
}
#endif
