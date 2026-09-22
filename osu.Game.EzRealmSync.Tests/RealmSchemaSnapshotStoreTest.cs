using NUnit.Framework;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Realm.Dynamic;
using osu.Game.EzRealmSync.Tests.TestInfrastructure;
using Realms;
using Realms.Schema;

namespace osu.Game.EzRealmSync.Tests
{
    /// <summary>
    /// schema 快照原语：落盘→读回必须一字不差（含 <see cref="PropertyType"/> 的原始 flags），
    /// 「官方事实来源」判据必须偏向"不是官方"，且采集失败绝不能影响调用方正在做的事。
    /// </summary>
    [TestFixture]
    public class RealmSchemaSnapshotStoreTest
    {
        [Test]
        public void Capture_writes_snapshot_keyed_by_disk_schema_version()
        {
            using var sandbox = new Sandbox();
            string realmPath = sandbox.CreateRealmFile(52_010);

            var captured = sandbox.Store.Capture(realmPath);

            Assert.Multiple(() =>
            {
                Assert.That(captured.DiskSchemaVersion, Is.EqualTo(52_010));
                Assert.That(sandbox.Store.FilePathFor(52_010), Does.EndWith("52010.schema.json"));
                Assert.That(File.Exists(sandbox.Store.FilePathFor(52_010)), Is.True);
                Assert.That(captured.SourcePath, Is.EqualTo(Path.GetFullPath(realmPath)));
            });

            // 采到的必须就是这份库现在的 schema，而不是"打开时顺手带上的模型"。
            using var session = DynamicRealmSession.OpenDynamic(realmPath, readOnly: true);
            var live = DynamicSchemaReader.Read(session);

            Assert.That(sandbox.Store.Load(52_010)!.Snapshot.FindDifferences(live), Is.Empty);
        }

        [Test]
        public void Saved_snapshot_round_trips_without_type_drift()
        {
            using var sandbox = new Sandbox();
            var schema = new RealmSchemaSnapshot(
            [
                new RealmClassSchema("Sample", false,
                [
                    new RealmPropertySchema("Ratio", PropertyType.Float, string.Empty, null, false, IndexType.None),
                    new RealmPropertySchema("NullableScore", PropertyType.Int | PropertyType.Nullable, string.Empty, null, false, IndexType.None),
                    new RealmPropertySchema("Tags", PropertyType.Array | PropertyType.String, string.Empty, null, false, IndexType.None),
                    new RealmPropertySchema("Parent", PropertyType.Object, "BeatmapSet", null, false, IndexType.None),
                    new RealmPropertySchema("Children", PropertyType.LinkingObjects, "Beatmap", "Parent", false, IndexType.None),
                ]),
            ]);

            sandbox.Store.Save(new RealmSchemaSnapshotFile(51, schema, "in-memory", DateTimeOffset.UtcNow));

            var loaded = sandbox.Store.Load(51);
            Assert.That(loaded, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(loaded!.Snapshot.Signature, Is.EqualTo(schema.Signature));
                Assert.That(loaded.Snapshot.FindDifferences(schema), Is.Empty);
                Assert.That(loaded.SourcePath, Is.EqualTo("in-memory"));

                // float 不能因 JSON 往返被归一成 double —— 这正是动态读写的已知陷阱。
                Assert.That(loaded.Snapshot.Find("Sample")!.Find("Ratio")!.Type, Is.EqualTo(PropertyType.Float));
                Assert.That(loaded.Snapshot.Find("Sample")!.Find("Tags")!.Type, Is.EqualTo(PropertyType.Array | PropertyType.String));
                Assert.That(loaded.Snapshot.Find("Sample")!.Find("Children")!.LinkOriginPropertyName, Is.EqualTo("Parent"));
            });

            Assert.That(Directory.GetFiles(sandbox.Store.DirectoryPath, "*.tmp"), Is.Empty, "原子替换后不该留下临时文件。");
        }

        [Test]
        public void TryCapture_keeps_first_snapshot_for_a_version()
        {
            using var sandbox = new Sandbox();
            string first = sandbox.CreateRealmFile(51, "first.realm");
            string second = sandbox.CreateRealmFile(51, "second.realm");

            Assert.That(sandbox.Store.TryCapture(first), Is.Not.Null);
            Assert.That(sandbox.Store.TryCapture(second), Is.Null, "同一版本已有快照时不该被后一次采集顶掉。");

            Assert.That(sandbox.Store.Load(51)!.SourcePath, Is.EqualTo(Path.GetFullPath(first)));

            Assert.That(sandbox.Store.TryCapture(second, overwrite: true), Is.Not.Null);
            Assert.That(sandbox.Store.Load(51)!.SourcePath, Is.EqualTo(Path.GetFullPath(second)));
        }

        [Test]
        public void TryCapture_returns_null_and_does_not_throw_for_missing_file()
        {
            using var sandbox = new Sandbox();
            string missing = Path.Combine(sandbox.Root, "nope.realm");

            Assert.That(sandbox.Store.TryCapture(missing), Is.Null);
            Assert.That(sandbox.Store.LoadAll(), Is.Empty);
        }

        [Test]
        public void LoadAll_lists_every_collected_version()
        {
            using var sandbox = new Sandbox();
            sandbox.Store.Capture(sandbox.CreateRealmFile(51, "a.realm"));
            sandbox.Store.Capture(sandbox.CreateRealmFile(52, "b.realm"));

            Assert.That(sandbox.Store.LoadAll().Select(f => f.DiskSchemaVersion), Is.EqualTo(new[] { 51, 52 }));
        }

        [Test]
        public void Official_baseline_requires_raw_official_version()
        {
            using var sandbox = new Sandbox();
            var official = knownOfficialSnapshot();

            sandbox.Store.Save(new RealmSchemaSnapshotFile(52, official, "official", DateTimeOffset.UtcNow));
            sandbox.Store.Save(new RealmSchemaSnapshotFile(52_010, official, "ez", DateTimeOffset.UtcNow));

            Assert.Multiple(() =>
            {
                Assert.That(sandbox.Store.TryLoadOfficialBaseline(52, out var snapshot, out int upstream), Is.True);
                Assert.That(snapshot!.Signature, Is.EqualTo(official.Signature));
                Assert.That(upstream, Is.EqualTo(52));

                // Ez 修订号非 0（52010）不是官方版号，哪怕内容看起来像官方。
                Assert.That(sandbox.Store.TryLoadOfficialBaseline(52_010, out _, out _), Is.False);
                Assert.That(sandbox.Store.TryLoadOfficialBaseline(53, out _, out _), Is.False, "没有这份快照。");
            });
        }

        /// <summary>版本号不再是闸门：白名单之外的官方号（如 49）照样能被当成官方 N 的事实来源。</summary>
        [Test]
        public void Official_baseline_does_not_gate_on_min_supported_version()
        {
            using var sandbox = new Sandbox();
            sandbox.Store.Save(new RealmSchemaSnapshotFile(49, knownOfficialSnapshot(), "official-49", DateTimeOffset.UtcNow));

            Assert.That(sandbox.Store.TryLoadOfficialBaseline(49, out _, out int upstream), Is.True);
            Assert.That(upstream, Is.EqualTo(49));
        }

        [Test]
        public void Official_baseline_rejects_ez_class_or_ez_column()
        {
            var withEzClass = new RealmSchemaSnapshot(
            [
                new RealmClassSchema("BeatmapSet", false, [property("ID"), property("Hash")]),
                new RealmClassSchema("EzDanEstimate", false, [property("ID")]),
            ]);

            var withEzColumn = knownOfficialSnapshot(withScoreProperty: "Passed");
            var withExplicitEzColumn = new RealmSchemaSnapshot(
            [
                new RealmClassSchema("Beatmap", false, [property("ID"), property("XxyStarRating")]),
            ]);

            Assert.Multiple(() =>
            {
                Assert.That(RealmSchemaSnapshotClassifier.TryGetOfficialUpstream(52, withEzClass, out _), Is.False);
                Assert.That(RealmSchemaSnapshotClassifier.TryGetOfficialUpstream(52, withEzColumn, out _), Is.False);
                Assert.That(RealmSchemaSnapshotClassifier.TryGetOfficialUpstream(52, withExplicitEzColumn, out _), Is.False);
            });
        }

        [Test]
        public void Ez_only_property_rules_cover_added_columns_and_ignore_unknown_classes()
        {
            Assert.Multiple(() =>
            {
                Assert.That(RealmSchemaSnapshotClassifier.IsEzOnlyProperty("Score", "Passed"), Is.True, "官方基线里没有 Score.Passed。");
                Assert.That(RealmSchemaSnapshotClassifier.IsEzOnlyProperty("Score", "TotalScore"), Is.False);
                Assert.That(RealmSchemaSnapshotClassifier.IsEzOnlyProperty("AnyClass", "XxyStarRating"), Is.True);
                Assert.That(RealmSchemaSnapshotClassifier.IsEzOnlyProperty("SomeUnknownClass", "Whatever"), Is.False,
                    "官方还有一批本工具没登记的类，不能因为「没见过这个类」就判成 Ez。");
            });
        }

        [Test]
        public void Drift_guard_reports_both_directions()
        {
            var before = new RealmSchemaSnapshot([new RealmClassSchema("Sample", false, [property("A")])]);
            var after = new RealmSchemaSnapshot([new RealmClassSchema("Sample", false, [property("B")])]);
            var unchanged = new RealmSchemaSnapshot([new RealmClassSchema("Sample", false, [property("A")])]);

            Assert.Multiple(() =>
            {
                Assert.That(RealmSchemaDriftGuard.Check(before, unchanged), Is.Empty);
                Assert.That(RealmSchemaDriftGuard.Check(before, after), Has.Count.EqualTo(2));
                Assert.That(() => RealmSchemaDriftGuard.EnsureUnchanged(before, after, "target.realm"),
                    Throws.TypeOf<InvalidOperationException>().With.Message.Contains("target.realm"));
            });
        }

        /// <summary>真实官方样本存在时，判据必须把它认成官方（而不是只会认手工构造的快照）。</summary>
        [Test]
        public void Real_official_sample_is_classified_as_official()
        {
            var samples = RealmSampleFixture.GetAllSamples()
                                            .Where(s => s.DiskSchemaKind == nameof(RealmDiskSchemaKind.PpyClient) && s.RealmFileExists)
                                            .ToList();

            if (samples.Count == 0)
                Assert.Ignore("未放置官方 Realm 样本。");

            using var sandbox = new Sandbox();

            foreach (var sample in samples)
            {
                using var copy = RealmSampleFixture.CreateWritableCopy(sample);
                var captured = sandbox.Store.Capture(copy.RealmFilePath);

                Assert.Multiple(() =>
                {
                    Assert.That(captured.Snapshot.ClassCount, Is.GreaterThan(10), $"样本 {sample.Kind} 的表数不合理。");
                    Assert.That(RealmSchemaSnapshotClassifier.TryGetOfficialUpstream(captured.DiskSchemaVersion, captured.Snapshot, out int upstream), Is.True,
                        $"样本 {sample.Kind}（版本 {captured.DiskSchemaVersion}）应被认成官方，却被判为 Ez：{EzFingerprintDetail(captured.Snapshot)}");
                    Assert.That(upstream, Is.GreaterThan(0));
                });
            }
        }

        private static string EzFingerprintDetail(RealmSchemaSnapshot snapshot) =>
            string.Join(", ", snapshot.Classes
                                      .SelectMany(c => c.Properties.Select(p => (Class: c.Name, Property: p.Name)))
                                      .Where(p => RealmSchemaSnapshotClassifier.IsEzOnlyProperty(p.Class, p.Property))
                                      .Select(p => $"{p.Class}.{p.Property}"));

        private static RealmPropertySchema property(string name) => new RealmPropertySchema(name, PropertyType.String, string.Empty, null, false, IndexType.None);

        /// <summary>按官方基线登记表构造"纯官方"快照，用来验证判据不会误判官方。</summary>
        private static RealmSchemaSnapshot knownOfficialSnapshot(string? withScoreProperty = null)
        {
            return new RealmSchemaSnapshot(OfficialBaselineSchema.KnownProperties.Select(pair => new RealmClassSchema(
                pair.Key,
                false,
                pair.Value
                    .Append(withScoreProperty != null && pair.Key == OfficialBaselineSchema.Score ? withScoreProperty : null)
                    .Where(name => name != null)
                    .Select(name => new RealmPropertySchema(name!, PropertyType.String, string.Empty, null, name == "ID", IndexType.None)))));
        }

        private sealed class Sandbox : IDisposable
        {
            private readonly string root;

            public Sandbox()
            {
                root = EzRealmSyncDataPaths.CreateTempSubdirectory("schema-snapshot-store");
                Store = new RealmSchemaSnapshotStore(Path.Combine(root, "snapshots"));
            }

            public string Root => root;

            public RealmSchemaSnapshotStore Store { get; }

            public string CreateRealmFile(ulong schemaVersion, string fileName = "client.realm")
            {
                string path = Path.Combine(root, fileName);
                RealmNativeLifetime.CreateEmptyRealmFile(path, schemaVersion);
                return path;
            }

            public void Dispose()
            {
                RealmNativeLifetime.Flush();

                try
                {
                    if (Directory.Exists(root))
                        Directory.Delete(root, recursive: true);
                }
                catch
                {
                    // 临时目录清理失败不影响测试结论。
                }
            }
        }
    }
}
