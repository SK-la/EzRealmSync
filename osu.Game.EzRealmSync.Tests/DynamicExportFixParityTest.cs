#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.EzRealmSync.IO;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Realm.Dynamic;
using osu.Game.EzRealmSync.Tests.TestInfrastructure;
using osu.Game.Scoring;

namespace osu.Game.EzRealmSync.Tests
{
    /// <summary>
    /// 导出与修复从 typed 模型换成动态读取后，**同一份库必须给出同一个结果**。
    ///
    /// 这三条对照覆盖的是产品里"看起来能动但会静默出错"的部分：
    /// 导出目录的文案与目标路径（用户拿到的文件名）、collection.db / scores.db 的二进制内容
    /// （stable 读得懂才算对）、非法字符诊断的条目集合。
    ///
    /// scores.db 那条是重点：动态版必须自己复刻判定数分派与 mods 位映射，
    /// 只比字段值不够，所以直接比两个文件的字节。
    /// </summary>
    [TestFixture]
    public class DynamicExportFixParityTest
    {
        [Test]
        public void Export_catalog_matches_typed_on_a_realistic_ez_realm()
        {
            string root = RealisticEzRealmSeeder.NewRoot("export-catalog-parity");
            int schema = RealmAccess.EzFileSchemaVersion;

            try
            {
                string path = Path.Combine(root, "client.realm");
                RealisticEzRealmSeeder.CreateCurrentEzRealm(path, schema);
                RealisticEzRealmSeeder.SeedFromOfficialSample(path, schema);

                foreach (ExportDataKind kind in Enum.GetValues<ExportDataKind>())
                {
                    List<RealmExportItem> dynamicItems;

                    using (var session = RealmAccessGateway.OpenDynamicForRead(path, out RealmSchemaSnapshot schemaSnapshot))
                        dynamicItems = DynamicExportCatalogBuilder.Build(session, schemaSnapshot, kind).Items.ToList();

                    List<RealmExportItem> typedItems;

                    using (var access = TypedRealmAccess.OpenForMutation(path, schema))
                        typedItems = TypedExportCatalogBuilder.Build(access, kind).Items.ToList();

                    assertSameItems(kind, typedItems, dynamicItems);
                }
            }
            finally
            {
                RealisticEzRealmSeeder.Cleanup(root);
            }
        }

        [Test]
        public void Collection_file_expansion_matches_typed_on_a_realistic_ez_realm()
        {
            string root = RealisticEzRealmSeeder.NewRoot("collection-export-parity");
            int schema = RealmAccess.EzFileSchemaVersion;

            try
            {
                string path = Path.Combine(root, "client.realm");
                RealisticEzRealmSeeder.CreateCurrentEzRealm(path, schema);
                RealisticEzRealmSeeder.SeedFromOfficialSample(path, schema);

                List<Guid> collectionIds;

                using (var access = TypedRealmAccess.OpenForMutation(path, schema))
                    collectionIds = access.Run(realm => realm.All<BeatmapCollection>().AsEnumerable().Select(c => c.ID).ToList());

                Assert.That(collectionIds, Is.Not.Empty, "样本没有同步进收藏夹，这条对照没有覆盖面。");

                List<RealmExportFileEntry> dynamicEntries;

                using (var session = RealmAccessGateway.OpenDynamicForRead(path, out RealmSchemaSnapshot schemaSnapshot))
                    dynamicEntries = DynamicExportExecutor.ResolveCollectionFiles(session, schemaSnapshot, collectionIds).ToList();

                List<RealmExportFileEntry> typedEntries;

                using (var access = TypedRealmAccess.OpenForMutation(path, schema))
                    typedEntries = TypedExportExecutor.ResolveCollectionFiles(access, collectionIds).ToList();

                assertSameEntries("Collection", typedEntries, dynamicEntries);
            }
            finally
            {
                RealisticEzRealmSeeder.Cleanup(root);
            }
        }

        [Test]
        public void Collection_db_export_matches_typed_bytes()
        {
            string root = RealisticEzRealmSeeder.NewRoot("collection-db-parity");
            int schema = RealmAccess.EzFileSchemaVersion;

            try
            {
                string path = Path.Combine(root, "client.realm");
                RealisticEzRealmSeeder.CreateCurrentEzRealm(path, schema);
                RealisticEzRealmSeeder.SeedFromOfficialSample(path, schema);

                List<Guid> ids;

                using (var access = TypedRealmAccess.OpenForMutation(path, schema))
                    ids = access.Run(realm => realm.All<BeatmapCollection>().AsEnumerable().Select(c => c.ID).ToList());

                Assert.That(ids, Is.Not.Empty, "样本没有同步进收藏夹，这条对照没有覆盖面。");

                string typedFile = Path.Combine(root, "typed-collection.db");
                string dynamicFile = Path.Combine(root, "dynamic-collection.db");

                int typedCount;
                using (var access = TypedRealmAccess.OpenForMutation(path, schema))
                    typedCount = TypedCollectionDbExporter.Export(access, ids, typedFile);

                int dynamicCount;
                using (var session = RealmAccessGateway.OpenDynamicForRead(path, out RealmSchemaSnapshot schemaSnapshot))
                    dynamicCount = RealmCollectionDbSync.Export(session, schemaSnapshot, ids, dynamicFile);

                Assert.That(dynamicCount, Is.EqualTo(typedCount), "导出的收藏夹数量不一致。");
                Assert.That(File.ReadAllBytes(dynamicFile), Is.EqualTo(File.ReadAllBytes(typedFile)),
                    "collection.db 字节不一致：stable 会按这些字节解析收藏夹。");
            }
            finally
            {
                RealisticEzRealmSeeder.Cleanup(root);
            }
        }

        [Test]
        public void Scores_db_export_matches_typed_bytes()
        {
            string root = RealisticEzRealmSeeder.NewRoot("scores-db-parity");
            int schema = RealmAccess.EzFileSchemaVersion;

            try
            {
                string path = Path.Combine(root, "client.realm");
                RealisticEzRealmSeeder.CreateCurrentEzRealm(path, schema);
                RealisticEzRealmSeeder.SeedFromOfficialSample(path, schema);

                List<Guid> ids;

                using (var access = TypedRealmAccess.OpenForMutation(path, schema))
                    ids = access.Run(realm => realm.All<ScoreInfo>().Where(s => !s.DeletePending).AsEnumerable().Select(s => s.ID).ToList());

                Assert.That(ids, Is.Not.Empty, "样本没有同步进成绩，这条对照没有覆盖面。");

                string typedFile = Path.Combine(root, "typed-scores.db");
                string dynamicFile = Path.Combine(root, "dynamic-scores.db");

                int typedCount;
                using (var access = TypedRealmAccess.OpenForMutation(path, schema))
                    typedCount = TypedScoresDbExporter.Export(access, ids, typedFile);

                int dynamicCount;
                using (var session = RealmAccessGateway.OpenDynamicForRead(path, out RealmSchemaSnapshot schemaSnapshot))
                    dynamicCount = DynamicScoresDbExporter.Export(session, schemaSnapshot, ids, dynamicFile);

                Assert.That(dynamicCount, Is.EqualTo(typedCount), "写出的成绩条数不一致。");
                Assert.That(File.ReadAllBytes(dynamicFile), Is.EqualTo(File.ReadAllBytes(typedFile)),
                    "scores.db 字节不一致：判定数分派或 mods 位映射与官方不同。");
            }
            finally
            {
                RealisticEzRealmSeeder.Cleanup(root);
            }
        }

        /// <summary>诊断条目（非法字符 / 缺失文件）动态版与 typed 版必须给出同一批。</summary>
        [Test]
        public void Illegal_character_scan_matches_typed_on_a_realistic_ez_realm()
        {
            string root = RealisticEzRealmSeeder.NewRoot("fix-scan-parity");
            int schema = RealmAccess.EzFileSchemaVersion;

            try
            {
                string path = Path.Combine(root, "client.realm");
                RealisticEzRealmSeeder.CreateCurrentEzRealm(path, schema);
                RealisticEzRealmSeeder.SeedFromOfficialSample(path, schema);
                seedIllegalCharacters(path, schema);

                var options = new RealmFixScanOptions
                {
                    ScanIllegalCharacters = true,
                    IllegalCharacters = [':', '*', '?'],
                };

                var dynamicIssues = new List<RealmFixIssue>();

                using (var session = RealmAccessGateway.OpenDynamicForRead(path, out RealmSchemaSnapshot schemaSnapshot))
                    RealmIllegalCharacterFixer.Scan(session, schemaSnapshot, dynamicIssues, options);

                var typedIssues = new List<RealmFixIssue>();

                using (var access = TypedRealmAccess.OpenForMutation(path, schema))
                    TypedIllegalCharacterScanner.Scan(access, typedIssues, options);

                Assert.That(describe(dynamicIssues), Is.EqualTo(describe(typedIssues)),
                    "动态扫描出的非法字符条目与 typed 不一致。");
            }
            finally
            {
                RealisticEzRealmSeeder.Cleanup(root);
            }
        }

        /// <summary>
        /// 修复落库后：typed 侧读到的值必须是修好的，且库的 schema 一字未变（只填单元格，不改结构）。
        /// </summary>
        [Test]
        public void Illegal_character_fix_writes_values_without_changing_schema()
        {
            string root = RealisticEzRealmSeeder.NewRoot("fix-apply");
            int schema = RealmAccess.EzFileSchemaVersion;

            try
            {
                string path = Path.Combine(root, "client.realm");
                RealisticEzRealmSeeder.CreateCurrentEzRealm(path, schema);
                RealisticEzRealmSeeder.SeedFromOfficialSample(path, schema);
                seedIllegalCharacters(path, schema);

                var options = new RealmFixScanOptions
                {
                    ScanIllegalCharacters = true,
                    IllegalCharacters = [':'],
                    IllegalCharacterReplacement = "_",
                };

                var snapshots = new RealmSchemaSnapshotStore(Path.Combine(root, "snapshots"));

                RealmSchemaSnapshot before = snapshots.Capture(path).Snapshot;
                var issues = new List<RealmFixIssue>();

                int applied;

                using (var session = RealmAccessGateway.OpenDynamicForWrite(path, out RealmSchemaSnapshot schemaSnapshot))
                {
                    RealmIllegalCharacterFixer.Scan(session, schemaSnapshot, issues, options);
                    Assert.That(issues, Is.Not.Empty, "没有造出含非法字符的数据，这条测试失去意义。");

                    applied = RealmIllegalCharacterFixer.Apply(session, schemaSnapshot, issues, CancellationToken.None);
                }

                Assert.That(applied, Is.GreaterThan(0), "动态修复没有改到任何一行。");

                RealmNativeLifetime.Flush();

                RealmSchemaSnapshot after = snapshots.Capture(path).Snapshot;
                RealmSchemaDriftGuard.EnsureUnchanged(before, after, path);

                using (var access = TypedRealmAccess.OpenForMutation(path, schema))
                {
                    access.Run(realm =>
                    {
                        var beatmaps = realm.All<BeatmapInfo>().AsEnumerable().ToList();

                        Assert.That(beatmaps.All(b => !b.Metadata.Tags.Contains(':') && !b.Metadata.Source.Contains(':')), Is.True,
                            "修复后库里仍有含 ':' 的元数据。");
                        Assert.That(beatmaps.Any(b => b.Metadata.Tags.Contains("parity_colon")), Is.True,
                            "修复后没有看到替换后的值。");
                    });
                }
            }
            finally
            {
                RealisticEzRealmSeeder.Cleanup(root);
            }
        }

        /// <summary>数据页软删：只置 DeletePending，不删行、不动 schema；Ez 列保持原值。</summary>
        [Test]
        public void Soft_delete_marks_pending_without_changing_schema_or_ez_columns()
        {
            string root = RealisticEzRealmSeeder.NewRoot("soft-delete");
            int schema = RealmAccess.EzFileSchemaVersion;

            try
            {
                string path = Path.Combine(root, "client.realm");
                RealisticEzRealmSeeder.CreateCurrentEzRealm(path, schema);
                RealisticEzRealmSeeder.SeedFromOfficialSample(path, schema);
                RealisticEzRealmSeeder.WriteEzOnlyValues(path, schema);

                Guid target;
                string ezRootBefore;

                using (var access = TypedRealmAccess.OpenForMutation(path, schema))
                {
                    target = access.Run(realm => realm.All<BeatmapSetInfo>().AsEnumerable().First(s => !s.DeletePending).ID);
                    ezRootBefore = access.Run(realm => realm.Find<BeatmapSetInfo>(target)!.ExternalContentRoot);
                }

                var snapshots = new RealmSchemaSnapshotStore(Path.Combine(root, "snapshots"));

                RealmSchemaSnapshot before = snapshots.Capture(path).Snapshot;

                int deleted;

                using (var session = RealmAccessGateway.OpenDynamicForWrite(path, out RealmSchemaSnapshot schemaSnapshot))
                    deleted = RealmBrowseEntityMutator.Delete(session, schemaSnapshot, RealmObjectClass.BeatmapSet, [target]);

                Assert.That(deleted, Is.EqualTo(1), "软删没有改到目标行。");

                RealmNativeLifetime.Flush();

                RealmSchemaSnapshot after = snapshots.Capture(path).Snapshot;
                RealmSchemaDriftGuard.EnsureUnchanged(before, after, path);

                using (var access = TypedRealmAccess.OpenForMutation(path, schema))
                {
                    // 值必须在 Run 里取出来：Run 返回后 realm 已关闭，拿着对象再读属性会抛 RealmClosedException。
                    (bool Found, bool DeletePending, string? EzRoot) state = access.Run(realm =>
                    {
                        var set = realm.Find<BeatmapSetInfo>(target);

                        return set == null
                            ? (false, false, null)
                            : (true, set.DeletePending, set.ExternalContentRoot);
                    });

                    Assert.That(state.Found, Is.True, "软删把整行删掉了，应该只置 DeletePending。");
                    Assert.That(state.DeletePending, Is.True, "DeletePending 没有置上。");
                    Assert.That(state.EzRoot, Is.EqualTo(ezRootBefore), "Ez 列在软删后变了。");
                }
            }
            finally
            {
                RealisticEzRealmSeeder.Cleanup(root);
            }
        }

        /// <summary>
        /// 导入 collection.db：只写 Name / BeatmapMD5Hashes / LastModified，schema 一字未变。
        ///
        /// 官方 51/52 的收藏夹主键是 <c>ID</c>（Guid）而不是 <c>Name</c>，合并必须按名称找；
        /// 这条同时守住「新建时自带一个新 Guid，不撞 Guid.Empty」。
        /// </summary>
        [Test]
        public void Collection_db_import_writes_without_changing_schema()
        {
            string root = RealisticEzRealmSeeder.NewRoot("collection-db-import");
            int schema = RealmAccess.EzFileSchemaVersion;

            try
            {
                string path = Path.Combine(root, "client.realm");
                RealisticEzRealmSeeder.CreateCurrentEzRealm(path, schema);
                RealisticEzRealmSeeder.SeedFromOfficialSample(path, schema);

                (string Name, string Hash) existing;

                using (var access = TypedRealmAccess.OpenForMutation(path, schema))
                {
                    existing = access.Run(realm =>
                    {
                        var withHashes = realm.All<BeatmapCollection>().AsEnumerable().FirstOrDefault(c => c.BeatmapMD5Hashes.Count > 0);

                        Assert.That(withHashes, Is.Not.Null, "样本里没有带 MD5 的收藏夹，这条导入测试失去覆盖面。");

                        return (withHashes!.Name, withHashes.BeatmapMD5Hashes.First());
                    });
                }

                var snapshots = new RealmSchemaSnapshotStore(Path.Combine(root, "snapshots"));
                RealmSchemaSnapshot before = snapshots.Capture(path).Snapshot;

                RealmCollectionDbImportResult result;

                using (var session = RealmAccessGateway.OpenDynamicForWrite(path, out RealmSchemaSnapshot schemaSnapshot))
                {
                    result = RealmCollectionDbSync.Import(session, schemaSnapshot,
                    [
                        new LegacyCollectionDbEntry(existing.Name, [existing.Hash, "00000000000000000000000000000000"]),
                        new LegacyCollectionDbEntry("parity-import-new", ["11111111111111111111111111111111"]),
                    ]);
                }

                Assert.That(result.CreatedCount, Is.EqualTo(1), "没有按名称新建收藏夹（同名的那条应该合并而不是新建）。");
                Assert.That(result.MergedCount, Is.EqualTo(1), "同名收藏夹没有走合并。");
                Assert.That(result.AddedHashCount, Is.EqualTo(2), "合并只应补进那条缺失的 MD5，新建那条算 1 条。");

                RealmNativeLifetime.Flush();

                RealmSchemaSnapshot after = snapshots.Capture(path).Snapshot;
                RealmSchemaDriftGuard.EnsureUnchanged(before, after, path);

                using (var access = TypedRealmAccess.OpenForMutation(path, schema))
                {
                    access.Run(realm =>
                    {
                        var merged = realm.All<BeatmapCollection>().AsEnumerable().Single(c => c.Name == existing.Name);
                        Assert.That(merged.BeatmapMD5Hashes, Does.Contain("00000000000000000000000000000000"));
                        Assert.That(merged.BeatmapMD5Hashes, Does.Contain(existing.Hash), "合并把原有 MD5 丢了。");

                        var created = realm.All<BeatmapCollection>().AsEnumerable().Single(c => c.Name == "parity-import-new");
                        Assert.That(created.BeatmapMD5Hashes, Is.EqualTo(new[] { "11111111111111111111111111111111" }));
                        Assert.That(created.ID, Is.Not.EqualTo(Guid.Empty), "新建收藏夹没有拿到主键。");
                    });
                }
            }
            finally
            {
                RealisticEzRealmSeeder.Cleanup(root);
            }
        }

        /// <summary>往元数据里写文档树不允许的字符：部分难度带 <c>:</c>，再加一条带 <c>*</c> 的，覆盖两个字符。</summary>
        private static void seedIllegalCharacters(string path, int schema)
        {
            using var access = TypedRealmAccess.OpenForMutation(path, schema);

            access.Write(realm =>
            {
                var beatmaps = realm.All<BeatmapInfo>().AsEnumerable().Take(2).ToList();

                Assert.That(beatmaps, Is.Not.Empty, "样本没有同步进难度，无法造非法字符数据。");

                beatmaps[0].Metadata.Tags = "parity:colon";
                beatmaps[0].Metadata.Source = "parity:source";

                if (beatmaps.Count > 1)
                    beatmaps[1].Metadata.Title = "parity*star";
            });

            RealmNativeLifetime.Flush();
        }

        private static void assertSameItems(ExportDataKind kind, List<RealmExportItem> typed, List<RealmExportItem> dynamic)
        {
            string[] describe(IEnumerable<RealmExportItem> items) => items
                .Select(i => $"{i.Id}|{i.Title}|{i.Artist}|{i.CollectionName}|{i.BeatmapCount}|{i.PlayerName}|{i.RelativePath}|{i.DestinationRelativePath}")
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray();

            Assert.That(describe(dynamic), Is.EqualTo(describe(typed)), $"{kind}：导出目录条目与 typed 不一致。");
        }

        private static void assertSameEntries(string kind, List<RealmExportFileEntry> typed, List<RealmExportFileEntry> dynamic)
        {
            string[] describe(IEnumerable<RealmExportFileEntry> entries) => entries
                .Select(e => $"{e.SourceRelative}|{e.DestinationRelative}|{e.CollectionFolder}")
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray();

            Assert.That(describe(dynamic), Is.EqualTo(describe(typed)), $"{kind}：待复制文件清单与 typed 不一致。");
        }

        private static string[] describe(IEnumerable<RealmFixIssue> issues) => issues
            .Select(i => $"{i.Kind}|{i.EntityKind}|{i.TargetEntityId}|{i.FieldName}|{i.CurrentValue}|{i.SuggestedValue}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();
    }
}
#endif
