#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Realm.Dynamic;
using osu.Game.EzRealmSync.Tests.TestInfrastructure;

namespace osu.Game.EzRealmSync.Tests
{
    /// <summary>
    /// 数据页浏览从 typed 模型换成动态读取之后，**同一份库的浏览结果必须逐格一致**。
    ///
    /// 这条对照比整表导出更贴产品：浏览是唯一一处「列是策展的、值是格式化过的」的界面，
    /// 动态版要自己复刻软删 / 隐藏过滤、日期与数值格式、链接列取对方主键这些细节，错一点用户就看到错数据。
    ///
    /// 有意保留的一处差别：谱面集 Status 列。typed 版本打印枚举名（Ranked 等），动态版打印库里存的数值——
    /// 与官方库的浏览（OfficialMirrorBrowseReader）一致，也避免把官方枚举表手抄进 Ez。故本测试对
    /// Status 列改比对「数值必须等于枚举的底层值」，而不是文本相等。
    /// </summary>
    [TestFixture]
    public class DynamicBrowseParityTest
    {
        [Test]
        public void Dynamic_browse_matches_typed_browse_on_a_realistic_ez_realm()
        {
            string root = RealisticEzRealmSeeder.NewRoot("browse-parity");
            int schema = RealmAccess.EzFileSchemaVersion;

            try
            {
                string path = Path.Combine(root, "client.realm");
                RealisticEzRealmSeeder.CreateCurrentEzRealm(path, schema);
                RealisticEzRealmSeeder.SeedFromOfficialSample(path, schema);
                RealisticEzRealmSeeder.WriteEzOnlyValues(path, schema);

                var file = new RealmFileEntry { Id = "browse-parity", DisplayName = "client.realm", FilePath = path };

                RealmSnapshot dynamicSnapshot = RealmBrowseSnapshotProvider.Read(file);

                RealmSnapshot typedSnapshot;

                using (var access = TypedRealmAccess.OpenForMutation(path, schema))
                    typedSnapshot = TypedBrowseSnapshotBuilder.Build(file, access);

                assertSameClasses(typedSnapshot, dynamicSnapshot);
            }
            finally
            {
                RealisticEzRealmSeeder.Cleanup(root);
            }
        }

        /// <summary>真实样本（官方 51 / Ez 旧库 / 当前 Ez 库）都要能整页读出来，且不许出现读不出的格子。</summary>
        [Test]
        public void Real_samples_browse_without_failing_on_any_class()
        {
            foreach (RealmSampleInfo sample in RealmSampleFixture.GetAllSamples().Where(s => s.RealmFileExists))
            {
                using var copy = RealmSampleFixture.CreateWritableCopy(sample);

                var file = new RealmFileEntry { Id = sample.Kind, DisplayName = sample.Kind, FilePath = copy.RealmFilePath };

                RealmSnapshot snapshot = RealmBrowseSnapshotProvider.Read(file);

                Assert.That(snapshot.Classes, Is.Not.Empty, $"{sample.Kind}：没有读到任何类型。");
                Assert.That(snapshot.Classes.Sum(c => c.Rows.Count), Is.GreaterThan(0), $"{sample.Kind}：一个类型里都没有行。");

                foreach (RealmClassGroup group in snapshot.Classes)
                {
                    Assert.That(group.Columns, Is.Not.Empty, $"{sample.Kind}.{group.Class}：没有列。");

                    // 空表是正常的（样本里就有空的收藏夹），但只要有行，每行都必须给出所有列的格子，
                    // 否则 UI 会静默缺格。
                    foreach (RealmBrowseRow row in group.Rows)
                    {
                        foreach (RealmColumnDefinition column in group.Columns)
                        {
                            Assert.That(row.Cells.ContainsKey(column.PropertyKey), Is.True,
                                $"{sample.Kind}.{group.Class}.{column.PropertyKey}：格子缺失。");
                        }
                    }
                }

                RealmNativeLifetime.Flush();
            }
        }

        private static void assertSameClasses(RealmSnapshot typed, RealmSnapshot dynamic)
        {
            Assert.Multiple(() =>
            {
                Assert.That(dynamic.Classes.Select(c => c.Class), Is.EqualTo(typed.Classes.Select(c => c.Class)),
                    "类型分组不一致（缺失的类型会整组分不出行）。");

                foreach (RealmClassGroup typedGroup in typed.Classes)
                {
                    RealmClassGroup dynamicGroup = dynamic.Classes.Single(c => c.Class == typedGroup.Class);

                    Assert.That(dynamicGroup.Columns.Select(c => c.PropertyKey), Is.EqualTo(typedGroup.Columns.Select(c => c.PropertyKey)),
                        $"{typedGroup.Class}：列不一致。");
                    Assert.That(dynamicGroup.Columns.Select(c => c.Header), Is.EqualTo(typedGroup.Columns.Select(c => c.Header)),
                        $"{typedGroup.Class}：列标题不一致。");
                    Assert.That(dynamicGroup.Columns.Select(c => c.TypeHint), Is.EqualTo(typedGroup.Columns.Select(c => c.TypeHint)),
                        $"{typedGroup.Class}：列类型提示不一致。");

                    // 行按 Id 配对比对：Realm 的枚举顺序不参与语义，行集合一致即可。
                    Dictionary<Guid, RealmBrowseRow> typedRows = typedGroup.Rows.ToDictionary(r => r.Id);
                    Dictionary<Guid, RealmBrowseRow> dynamicRows = dynamicGroup.Rows.ToDictionary(r => r.Id);

                    Assert.That(dynamicRows.Keys, Is.EquivalentTo(typedRows.Keys), $"{typedGroup.Class}：行不一致。");

                    foreach ((Guid id, RealmBrowseRow typedRow) in typedRows)
                    {
                        if (!dynamicRows.TryGetValue(id, out RealmBrowseRow? dynamicRow))
                            continue;

                        foreach (RealmColumnDefinition column in typedGroup.Columns)
                        {
                            string typedCell = typedRow.Cells[column.PropertyKey];
                            string dynamicCell = dynamicRow.Cells[column.PropertyKey];

                            if (typedGroup.Class == RealmObjectClass.BeatmapSet && column.PropertyKey == "Status")
                            {
                                // 显示差别是有意的：动态侧给数值，typed 侧给枚举名。比对数值等价。
                                Assert.That(
                                    dynamicCell,
                                    Is.EqualTo(((int)Enum.Parse<BeatmapOnlineStatus>(typedCell)).ToString()),
                                    $"{typedGroup.Class}.Status：动态显示值不等于枚举底层值。");
                                continue;
                            }

                            Assert.That(dynamicCell, Is.EqualTo(typedCell),
                                $"{typedGroup.Class}[{id}]/{column.PropertyKey}：浏览结果不一致。");
                        }
                    }
                }
            });
        }
    }
}
#endif
