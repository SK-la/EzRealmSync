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

        private static void createOfficialRealmViaWorker(string path, int schema, Guid setId, Guid beatmapId, string title)        {
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
                        Beatmaps =
                        [
                            new OfficialBeatmapDto
                            {
                                ID = beatmapId,
                                DifficultyName = "Normal",
                                RulesetShortName = "osu",
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
            });
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
