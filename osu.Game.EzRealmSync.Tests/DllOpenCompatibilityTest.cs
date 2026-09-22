#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Realm.Readers;
using osu.Game.EzRealmSync.Tests.TestInfrastructure;

namespace osu.Game.EzRealmSync.Tests
{
    /// <summary>
    /// 动态同步产物必须能被「写出该产物 schema 的 osu.Game.dll」以 pinned disk schema 打开
    /// （<c>OpenWithoutMigration</c>），且产物文件头 schema 与同步前一致。
    ///
    /// 验证通道必须与产物 schema 同源：官方产物 → official-write Worker（OfficialSchema 镜像，
    /// 按目标 upstream 建 schema）；Ez 产物 → 与产物同版本的 Ez DLL（进程内 bundled 模型，
    /// 或 readers/&lt;schema&gt;/lib + read-sidecar）。
    /// 不要在 Ez 产物上用**不同 Ez 修订**的 reader 包做验证——那会触发 MigrationNeeded，
    /// 与同步无关（这正是「DLL 不是同步前提」的含义）。
    /// </summary>
    [TestFixture]
    public class DllOpenCompatibilityTest
    {
        private const int official_schema = 51;
        private const int ez_sample_schema = 51_007;

        private static readonly Guid source_set_id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid source_beatmap_id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid source_collection_id = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid source_skin_id = Guid.Parse("44444444-4444-4444-4444-444444444444");

        [Test]
        public void Official_target_after_baseline_sync_opens_with_official_dll()
        {
            string root = newRoot("official");

            try
            {
                string sourcePath = Path.Combine(root, "source.realm");
                string targetPath = Path.Combine(root, "target.realm");

                createOfficialRealm(sourcePath, official_schema, withBaseline: true);
                createOfficialRealm(targetPath, official_schema, withBaseline: false);

                syncBaseline(sourcePath, targetPath, [source_set_id, source_collection_id, source_skin_id]);

                AssertSchemaUnchanged(targetPath, official_schema);
                AssertBaselineCopied(targetPath, official_schema);

                assertOpensWithOfficialDll(targetPath, official_schema);

                AssertSchemaUnchanged(targetPath, official_schema);
            }
            finally
            {
                cleanup(root);
            }
        }

        [Test]
        public void Ez_source_synced_into_official_target_opens_with_official_dll()
        {
            string root = newRoot("ez-to-official");

            RealmSampleInfo? sample = tryGetEzSample();
            if (sample == null)
                Assert.Ignore($"未放置 schema {ez_sample_schema} 的 Ez 样本，跳过 Ez → 官方的 DLL 打开验证。");

            using var source = RealmSampleFixture.CreateWritableCopy(sample);

            try
            {
                string targetPath = Path.Combine(root, "target.realm");

                Guid[] ids = DynamicBaselineReader
                             .ReadDiffSnapshot(source.RealmFilePath, [EntityKind.BeatmapSet])
                             .Enumerate(EntityKind.BeatmapSet)
                             .Take(3)
                             .Select(e => e.Id)
                             .ToArray();

                if (ids.Length == 0)
                    Assert.Ignore("Ez 样本没有可同步的谱面集。");

                createOfficialRealm(targetPath, official_schema, withBaseline: false);
                syncBaseline(source.RealmFilePath, targetPath, ids);

                AssertSchemaUnchanged(targetPath, official_schema);

                int copied = DynamicBaselineReader
                             .ReadDiffSnapshot(targetPath, [EntityKind.BeatmapSet])
                             .Entities.Count;
                Assert.That(copied, Is.GreaterThanOrEqualTo(ids.Length), "Ez → 官方 没有拷出谱面集。");

                assertOpensWithOfficialDll(targetPath, official_schema);

                AssertSchemaUnchanged(targetPath, official_schema);
            }
            finally
            {
                cleanup(root);
            }
        }

        [Test]
        public void Official_source_synced_into_current_ez_target_opens_with_current_dll()
        {
            string root = newRoot("official-to-ez");
            int targetSchema = RealmAccess.EzFileSchemaVersion;

            try
            {
                string sourcePath = Path.Combine(root, "source.realm");
                string targetPath = Path.Combine(root, "target.realm");

                createOfficialRealm(sourcePath, official_schema, withBaseline: true);
                createCurrentEzRealm(targetPath, targetSchema);

                syncBaseline(sourcePath, targetPath, [source_set_id, source_collection_id, source_skin_id]);

                AssertSchemaUnchanged(targetPath, targetSchema);
                AssertBaselineCopied(targetPath, targetSchema);

                // 目标 schema == bundled Ez schema，用当前 DLL 进程内 pinned 打开。
                using (var access = RealmAccessGateway.OpenForMutation(targetPath, targetSchema))
                    access.Run(_ => { });

                AssertSchemaUnchanged(targetPath, targetSchema);
            }
            finally
            {
                cleanup(root);
            }
        }

        [Test]
        public void Current_ez_target_after_baseline_sync_opens_in_sidecar_with_matching_reader_package()
        {
            string root = newRoot("ez-sidecar");
            int targetSchema = RealmAccess.EzFileSchemaVersion;

            string? repoRoot = findRepoRoot();
            string libDirectory = repoRoot == null
                ? string.Empty
                : Path.Combine(repoRoot, "readers", targetSchema.ToString(), "lib");

            if (!File.Exists(Path.Combine(libDirectory, "osu.Game.dll")))
                Assert.Ignore($"没有 schema {targetSchema} 的 reader 包（先运行 scripts/Sync-ReaderLibs.ps1 并更新 sync-libs.config.json）。");

            string worker = RealmReadSidecarRunner.ResolveWorkerExecutablePathForTests();
            if (!File.Exists(worker))
                Assert.Ignore($"ReadSidecar Worker 未复制到测试输出：{worker}");

            try
            {
                string sourcePath = Path.Combine(root, "source.realm");
                string targetPath = Path.Combine(root, "target.realm");

                createOfficialRealm(sourcePath, official_schema, withBaseline: true);
                createCurrentEzRealm(targetPath, targetSchema);

                syncBaseline(sourcePath, targetPath, [source_set_id]);
                AssertSchemaUnchanged(targetPath, targetSchema);

                var package = new RealmReaderPackageInfo
                {
                    Id = $"test-{targetSchema}",
                    DisplayName = $"test-{targetSchema}",
                    Profile = "ez",
                    DiskSchemaVersions = [targetSchema],
                    PackageDirectory = libDirectory,
                    LibDirectory = libDirectory,
                };

                var job = new RealmReadJob
                {
                    ReaderLibDirectory = libDirectory,
                    SharedLibDirectory = Path.GetDirectoryName(worker),
                    RealmFilePath = targetPath,
                    PinnedDiskSchemaVersion = targetSchema,
                    Profile = "ez",
                };

                var result = RealmReadSidecarRunner.ReadDiffSnapshot(package, job);
                Assert.That(result.Success, Is.True);
                Assert.That(result.Entities.Any(e => e.Id == source_set_id), Is.True, "sidecar 没读到同步过去的谱面集。");

                AssertSchemaUnchanged(targetPath, targetSchema);
            }
            finally
            {
                cleanup(root);
            }
        }

        private static void syncBaseline(string sourcePath, string targetPath, IReadOnlyList<Guid> itemIds)
        {
            var bundle = DynamicBaselineReader.ExportByIds(sourcePath, itemIds);
            Assert.That(bundle.BeatmapSets.Count + bundle.Beatmaps.Count + bundle.Collections.Count + bundle.Skins.Count, Is.GreaterThan(0), "源库没有导出任何基线对象。");

            DynamicBaselineFileCopier.CopyMissing(sourcePath, targetPath, bundle);

            var result = DynamicBaselineWriter.Apply(
                new ApplyRequest { ItemIds = itemIds, CreateBackup = false },
                bundle,
                targetPath);

            Assert.That(result.AppliedCount, Is.GreaterThan(0), "动态写入没有落到任何对象。");
        }

        private static void AssertBaselineCopied(string targetPath, int targetSchema)
        {
            using var session = DynamicRealmSession.OpenDynamic(targetPath, readOnly: true);

            var set = DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.BeatmapSet, source_set_id);
            Assert.That(set, Is.Not.Null, "目标库没有同步过去的谱面集。");
            Assert.That(DynamicRealmAccess.GetString(set, "Hash"), Is.EqualTo("set-hash"));

            var beatmap = DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.Beatmap, source_beatmap_id);
            Assert.That(beatmap, Is.Not.Null, "目标库没有同步过去的难度。");

            Assert.That(DynamicRealmAccess.EnumerateObjects(set, "Files").Any(), Is.True, "谱面集没有 File 用法。");
            Assert.That(DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.File, "set-file-hash"), Is.Not.Null, "目标库没有 File 索引行。");

            if (session.HasClass(OfficialBaselineSchema.BeatmapCollection))
                Assert.That(DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.BeatmapCollection, source_collection_id), Is.Not.Null, "目标库没有同步过去的收藏夹。");

            if (session.HasClass(OfficialBaselineSchema.Skin))
                Assert.That(DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.Skin, source_skin_id), Is.Not.Null, "目标库没有同步过去的皮肤。");
        }

        private static void AssertSchemaUnchanged(string realmPath, int expectedSchema)
        {
            int? actual = RealmDiskSchemaReader.TryReadSchemaVersion(realmPath, out string? error);
            Assert.That(actual, Is.EqualTo(expectedSchema), $"文件头 schema 被改动：{error}");
        }

        private static void assertOpensWithOfficialDll(string realmPath, int pinnedSchema)
        {
            string worker = OfficialWriteProcessRunner.ResolveWorkerExecutablePathForTests();
            if (!File.Exists(worker))
                Assert.Ignore($"Official Worker 未复制到测试输出：{worker}");

            var result = OfficialReadProcessRunner.Read(new RealmReadJob
            {
                ReaderLibDirectory = string.Empty,
                RealmFilePath = realmPath,
                PinnedDiskSchemaVersion = pinnedSchema,
                Profile = "official",
            });

            Assert.That(result.Success, Is.True);
        }

        private static void createOfficialRealm(string path, int schema, bool withBaseline)
        {
            var job = new OfficialConvertJob
            {
                TargetUpstreamSchema = schema,
                TargetRealmPath = path,
                Rulesets =
                [
                    new OfficialRulesetDto { ShortName = "osu", OnlineID = 0, Name = "osu!" },
                ],
            };

            if (withBaseline)
            {
                job.BeatmapSets.Add(new OfficialBeatmapSetDto
                {
                    ID = source_set_id,
                    Hash = "set-hash",
                    DateAdded = DateTimeOffset.UtcNow,
                    Files = [new OfficialNamedFileDto { Hash = "set-file-hash", Filename = "audio.mp3" }],
                    Beatmaps =
                    [
                        new OfficialBeatmapDto
                        {
                            ID = source_beatmap_id,
                            DifficultyName = "Normal",
                            RulesetShortName = "osu",
                            Hash = "bm-hash",
                            MD5Hash = "bm-md5",
                            Metadata = new OfficialBeatmapMetadataDto
                            {
                                Title = "Baseline Song",
                                Artist = "Artist",
                                Author = new OfficialRealmUserDto { Username = "mapper" },
                            },
                        },
                    ],
                });

                job.Collections.Add(new OfficialCollectionDto
                {
                    ID = source_collection_id,
                    Name = "Baseline Collection",
                    LastModified = DateTimeOffset.UtcNow,
                    BeatmapMD5Hashes = ["bm-md5"],
                });

                job.Skins.Add(new OfficialSkinDto
                {
                    ID = source_skin_id,
                    Name = "Baseline Skin",
                    Creator = "creator",
                    InstantiationInfo = "osu.Game.Skinning.LegacySkin",
                    Files = [new OfficialNamedFileDto { Hash = "skin-file-hash", Filename = "hitcircle.png" }],
                });
            }

            OfficialWriteProcessRunner.Run(job);
        }

        /// <summary>用 bundled Ez 模型在同版本全新库上落 schema；这是「当前 Ez 客户端」写出的等价产物。</summary>
        private static void createCurrentEzRealm(string path, int schema)
        {
            RealmNativeLifetime.CreateEmptyRealmFile(path, (ulong)schema);

            using (var access = RealmAccessGateway.OpenForMutation(path, schema))
                access.Run(_ => { });

            RealmNativeLifetime.Flush();
        }

        private static RealmSampleInfo? tryGetEzSample() =>
            RealmSampleFixture.GetAllSamples()
                              .FirstOrDefault(s => s.RealmFileExists
                                                   && s.DiskSchemaKind == nameof(RealmDiskSchemaKind.EzExtended)
                                                   && RealmDiskSchemaReader.TryReadSchemaVersion(s.RealmFilePath) == ez_sample_schema);

        private static string newRoot(string name)
        {
            string root = Path.Combine(Path.GetTempPath(), "EzRealmSyncDllOpen", name + "-" + Guid.NewGuid().ToString("N"));
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

        private static string? findRepoRoot()
        {
            string? current = AppContext.BaseDirectory;

            for (int i = 0; i < 10 && current != null; i++)
            {
                if (File.Exists(Path.Combine(current, "EzRealmSync.sln")))
                    return current;

                current = Directory.GetParent(current)?.FullName;
            }

            return null;
        }
    }
}
#endif
