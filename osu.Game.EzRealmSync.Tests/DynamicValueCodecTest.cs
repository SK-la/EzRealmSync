using System.Collections;
using System.Globalization;
using NUnit.Framework;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Realm.Dynamic;
using osu.Game.EzRealmSync.Tests.TestInfrastructure;
using Realms;
using Realms.Schema;

namespace osu.Game.EzRealmSync.Tests
{
    /// <summary>
    /// 动态引擎（schema 枚举 + 按列类型读写）的对照测试。
    ///
    /// 用真实官方样本而不是临时构造的库：样本里有嵌入对象、链接、列表、可空列与 float 列，
    /// 正是「<c>GetList&lt;T&gt;</c> 不校验元素类型」「<c>AsAny</c> 把 float 归一成 double」
    /// 这两个陷阱的现场。样本一律只读打开，写测试用复制件。
    /// </summary>
    [TestFixture]
    public class DynamicValueCodecTest
    {
        private const int max_rows_per_class = 20;

        [Test]
        public void Schema_enumeration_reads_real_official_sample()
        {
            string? sample = tryResolveOfficialSample();
            if (sample == null)
                Assert.Ignore("缺少官方 Realm 样本。");

            using var session = openReadOnly(sample, out int version);
            var schema = DynamicSchemaReader.Read(session);

            Assert.That(version, Is.GreaterThan(0));
            Assert.That(schema.ClassCount, Is.GreaterThan(10), "真实库不可能只有十来张表。");
            Assert.That(schema.Signature, Is.Not.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(schema.TryFindClass("BeatmapSet", out var set), Is.True);
                Assert.That(set!.PrimaryKeyProperty, Is.EqualTo("ID"));
                Assert.That(set.Find("ID")!.ElementType, Is.EqualTo(PropertyType.Guid));
                Assert.That(set.Find("ID")!.IsPrimaryKey, Is.True);
                Assert.That(set.Find("DateSubmitted")!.IsNullable, Is.True, "DateSubmitted 是可空时间列。");
                Assert.That(set.Find("Hash")!.ElementType, Is.EqualTo(PropertyType.String));
                Assert.That(set.Find("Protected")!.IsNullable, Is.False, "Protected 是必填 bool 列。");

                // 列表：元素类型与目标类必须从 schema 读出来，不能猜。
                Assert.That(set.Find("Beatmaps")!.IsCollection, Is.True);
                Assert.That(set.Find("Beatmaps")!.ElementType, Is.EqualTo(PropertyType.Object));
                Assert.That(set.Find("Beatmaps")!.ObjectType, Is.EqualTo("Beatmap"));

                Assert.That(schema.TryFindClass("Beatmap", out var beatmap), Is.True);
                Assert.That(beatmap!.Find("Metadata")!.ElementType, Is.EqualTo(PropertyType.Object));
                Assert.That(beatmap.Find("Metadata")!.ObjectType, Is.EqualTo("BeatmapMetadata"));
                Assert.That(beatmap.Find("Hidden")!.IsNullable, Is.False);

                // 嵌入类：RealmUser / BeatmapDifficulty 是 EmbeddedObject，必须被标出来。
                Assert.That(schema.TryFindClass("RealmUser", out var user), Is.True);
                Assert.That(user!.IsEmbedded, Is.True);
                Assert.That(user.PrimaryKeyProperty, Is.Null, "嵌入类没有主键。");

                Assert.That(schema.TryFindClass("BeatmapDifficulty", out var difficulty), Is.True);
                Assert.That(difficulty!.IsEmbedded, Is.True);
                Assert.That(difficulty.Find("DrainRate")!.ElementType, Is.EqualTo(PropertyType.Float));
            });
        }

        [Test]
        public void Schema_signature_is_stable_across_reopen()
        {
            string? sample = tryResolveOfficialSample();
            if (sample == null)
                Assert.Ignore("缺少官方 Realm 样本。");

            string first;
            using (var session = openReadOnly(sample, out _))
                first = DynamicSchemaReader.Read(session).Signature;

            string second;
            using (var session = openReadOnly(sample, out _))
                second = DynamicSchemaReader.Read(session).Signature;

            Assert.That(second, Is.EqualTo(first), "同一份库的 schema 指纹必须可复现。");
        }

        /// <summary>
        /// 引擎的核心保证：把每一列按 schema 读出来、再原样写回，值与 schema 都不许变。
        /// 覆盖标量、标量列表、非嵌入单链接与其列表；嵌入对象列只读不写（重建嵌入式对象属于复制语义）。
        /// </summary>
        [Test]
        public void Every_writable_column_round_trips_without_schema_or_value_drift()
        {
            string? sample = tryResolveOfficialSample();
            if (sample == null)
                Assert.Ignore("缺少官方 Realm 样本。");

            string workPath = copyToTemp(sample);

            try
            {
                string beforeSignature;
                Dictionary<string, string> before = new(StringComparer.Ordinal);

                using (var session = openReadOnly(workPath, out int version))
                {
                    var schema = DynamicSchemaReader.Read(session);
                    beforeSignature = schema.Signature;
                    captureAll(session.Realm, schema, before);
                }

                Assert.That(before, Is.Not.Empty, "样本里没有可读的行。");
                Assert.That(before.Count, Is.GreaterThan(20), "样本里可读的行太少，这个测试就失去意义了。");

                using (var session = DynamicRealmSession.OpenDynamic(workPath, readOnly: false))
                {
                    var schema = DynamicSchemaReader.Read(session);

                    using (var transaction = session.Realm.BeginWrite())
                    {
                        rewriteAll(session.Realm, schema);
                        transaction.Commit();
                    }
                }

                RealmNativeLifetime.Flush();

                using (var session = openReadOnly(workPath, out _))
                {
                    var schema = DynamicSchemaReader.Read(session);
                    var after = new Dictionary<string, string>(StringComparer.Ordinal);
                    captureAll(session.Realm, schema, after);

                    Assert.That(schema.Signature, Is.EqualTo(beforeSignature), "写入前后 schema 变了。");

                    foreach ((string key, string expected) in before)
                    {
                        Assert.That(after.TryGetValue(key, out string? actual), Is.True, $"{key} 在写回后消失了。");
                        Assert.That(actual, Is.EqualTo(expected), $"{key} 写回后值变了。");
                    }
                }
            }
            finally
            {
                RealmNativeLifetime.Flush();
                tryDelete(workPath);
            }
        }

        [Test]
        public void Float_columns_are_not_normalized_to_double()
        {
            string? sample = tryResolveOfficialSample();
            if (sample == null)
                Assert.Ignore("缺少官方 Realm 样本。");

            using var session = openReadOnly(sample, out _);
            var schema = DynamicSchemaReader.Read(session);
            Assert.That(schema.TryFindClass("BeatmapDifficulty", out var difficulty), Is.True);

            RealmPropertySchema drain = difficulty!.Find("DrainRate")!;
            Assert.That(drain.ElementType, Is.EqualTo(PropertyType.Float));

            IRealmObjectBase? embedded = null;
            int scanned = 0;

            foreach (var beatmap in DynamicRealmAccess.All(session.Realm, "Beatmap"))
            {
                embedded = DynamicRealmAccess.Get<IRealmObjectBase>(beatmap, "Difficulty");

                if (embedded != null || ++scanned >= 50)
                    break;
            }

            if (embedded == null)
                Assert.Ignore("样本里没有带难度的行。");

            object? value = DynamicValueCodec.Read(embedded!, drain);
            Assert.That(value, Is.TypeOf<float>(), "float 列被 AsAny 归一成了 double，写回会改列语义。");
            Assert.That(DynamicValueCodec.Coerce(value, PropertyType.Float), Is.EqualTo(value));
        }

        [Test]
        public void Int_columns_read_back_as_long_and_keep_null()
        {
            string? sample = tryResolveOfficialSample();
            if (sample == null)
                Assert.Ignore("缺少官方 Realm 样本。");

            using var session = openReadOnly(sample, out _);
            var schema = DynamicSchemaReader.Read(session);

            var set = firstRow(session.Realm, "BeatmapSet");
            if (set == null)
                Assert.Ignore("样本里没有谱面集。");

            var onlineId = schema.Find("BeatmapSet")!.Find("OnlineID")!;
            object? value = DynamicValueCodec.Read(set, onlineId);

            if (value != null)
                Assert.That(value, Is.TypeOf<long>(), "整型列一律归一成 long。");

            Assert.That(DynamicValueCodec.Coerce(7, PropertyType.Int), Is.EqualTo(7L));
        }

        [Test]
        public void Scalar_list_uses_schema_element_type()
        {
            string? sample = tryResolveOfficialSample();
            if (sample == null)
                Assert.Ignore("缺少官方 Realm 样本。");

            using var session = openReadOnly(sample, out _);
            var schema = DynamicSchemaReader.Read(session);

            (RealmPropertySchema Property, IReadOnlyList<object?> Values)? found = null;

            foreach (var classSchema in schema.Classes.Where(c => !c.IsEmbedded))
            {
                foreach (var listProperty in classSchema.Properties.Where(p => p.IsCollection && !p.IsDictionary && isScalar(p.ElementType)))
                {
                    if (firstRowWithValues(session.Realm, classSchema.Name, listProperty) is not { } row)
                        continue;

                    found = (listProperty, (IReadOnlyList<object?>)DynamicValueCodec.Read(row, listProperty)!);
                    break;
                }

                if (found != null)
                    break;
            }

            if (found == null)
                Assert.Ignore("样本里没有非空的标量列表列。");

            Type expected = found.Value.Property.ElementType switch
            {
                PropertyType.Int => typeof(long),
                PropertyType.String => typeof(string),
                PropertyType.Bool => typeof(bool),
                PropertyType.Double => typeof(double),
                PropertyType.Float => typeof(float),
                _ => throw new InvalidOperationException($"未覆盖的元素类型 {found.Value.Property.ElementType}。"),
            };

            Assert.That(
                found.Value.Values.All(item => item == null || item.GetType() == expected),
                Is.True,
                $"列表 {found.Value.Property.Name}（{found.Value.Property.DescribeType()}）的元素应读成 {expected.Name}。");
        }

        /// <summary>
        /// osu! 的 Realm schema 里目前没有反向链接列（<c>RealmFile.Usages</c> 不进 schema），
        /// 所以用构造的列验证「反向链接只读」这条契约。
        /// </summary>
        [Test]
        public void Linking_objects_column_is_read_only()
        {
            string? sample = tryResolveOfficialSample();
            if (sample == null)
                Assert.Ignore("缺少官方 Realm 样本。");

            using var session = openReadOnly(sample, out _);
            var row = firstRow(session.Realm, "BeatmapSet");

            Assert.That(row, Is.Not.Null, "样本里没有谱面集行。");

            var backlink = new RealmPropertySchema("Usages", PropertyType.LinkingObjects, "RealmNamedFileUsage", "File", false, IndexType.None);

            Assert.Multiple(() =>
            {
                Assert.That(backlink.ElementType, Is.EqualTo(PropertyType.LinkingObjects));
                Assert.That(backlink.IsCollection, Is.False);
                Assert.That(backlink.DescribeType(), Is.EqualTo("LinkingObjects"));
                Assert.That(() => DynamicValueCodec.Write(row!, backlink, null), Throws.InvalidOperationException);
            });
        }

        private static bool isScalar(PropertyType elementType) =>
            elementType != PropertyType.Object && elementType != PropertyType.LinkingObjects && elementType != PropertyType.RealmValue;

        private static IRealmObjectBase? firstRowWithValues(Realms.Realm realm, string className, RealmPropertySchema property)
        {
            foreach (var row in DynamicRealmAccess.All(realm, className))
            {
                if (DynamicValueCodec.Read(row, property) is IReadOnlyList<object?> { Count: > 0 })
                    return row;
            }

            return null;
        }

        [Test]
        public void Int_list_column_round_trips_long_elements()
        {
            string? sample = tryResolveOfficialSample();
            if (sample == null)
                Assert.Ignore("缺少官方 Realm 样本。");

            using (var session = openReadOnly(sample, out _))
            {
                var pauses = DynamicSchemaReader.Read(session).Find("Score")?.Find("Pauses");
                Assert.That(pauses, Is.Not.Null, "官方 Score 表没有 Pauses 列。");

                Assert.Multiple(() =>
                {
                    Assert.That(pauses!.IsCollection, Is.True);
                    Assert.That(pauses.ElementType, Is.EqualTo(PropertyType.Int));
                    Assert.That(pauses.DescribeType(), Is.EqualTo("Array<Int>"), "flags 枚举不能直接 ToString。");
                });
            }

            string workPath = copyToTemp(sample);

            try
            {
                long[] written = [11, 22, 33];
                Guid scoreId;

                using (var session = DynamicRealmSession.OpenDynamic(workPath, readOnly: false))
                {
                    var pauses = DynamicSchemaReader.Read(session).Find("Score")!.Find("Pauses")!;
                    var row = firstRow(session.Realm, "Score");
                    Assert.That(row, Is.Not.Null, "样本里没有成绩行。");

                    scoreId = row!.DynamicApi.Get<Guid>("ID");

                    using var transaction = session.Realm.BeginWrite();
                    DynamicValueCodec.Write(row, pauses, written);
                    transaction.Commit();
                }

                RealmNativeLifetime.Flush();

                using (var session = openReadOnly(workPath, out _))
                {
                    var pauses = DynamicSchemaReader.Read(session).Find("Score")!.Find("Pauses")!;
                    var row = DynamicRealmAccess.Find(session.Realm, "Score", scoreId);
                    Assert.That(row, Is.Not.Null);

                    // RealmValue.AsAny 会把整数归一成 long，IList<int> 的窄元素类型只能靠 schema 分派才拿得到。
                    var values = (IReadOnlyList<object?>)DynamicValueCodec.Read(row!, pauses)!;

                    Assert.That(
                        values.Select(item => item is long number ? (long?)number : null),
                        Is.EqualTo(written.Select(number => (long?)number)));
                }
            }
            finally
            {
                RealmNativeLifetime.Flush();
                tryDelete(workPath);
            }
        }

        [Test]
        public void Non_embedded_link_and_its_list_round_trip()
        {
            string? sample = tryResolveOfficialSample();
            if (sample == null)
                Assert.Ignore("缺少官方 Realm 样本。");

            using var session = openReadOnly(sample, out _);
            var schema = DynamicSchemaReader.Read(session);

            IRealmObjectBase? set = null;

            // 不用 Realm 的 LINQ（谓词会被翻译成原生查询，这里没有可翻译的表达式）。
            foreach (var candidate in DynamicRealmAccess.All(session.Realm, "BeatmapSet"))
            {
                if (!DynamicRealmAccess.EnumerateObjects(candidate, "Beatmaps").Any())
                    continue;

                set = candidate;
                break;
            }

            if (set == null)
                Assert.Ignore("样本里没有带难度的谱面集。");

            var beatmaps = schema.Find("BeatmapSet")!.Find("Beatmaps")!;
            object? value = DynamicValueCodec.Read(set, beatmaps);

            Assert.That(value, Is.InstanceOf<IReadOnlyList<object?>>());
            var list = (IReadOnlyList<object?>)value!;
            Assert.That(list, Is.Not.Empty);
            Assert.That(list.All(item => item is IRealmObjectBase), Is.True, "对象列表的元素必须是 Realm 对象句柄。");

            var first = (IRealmObjectBase)list[0]!;
            var parentLink = schema.Find("Beatmap")!.Find("BeatmapSet")!;
            object? parent = DynamicValueCodec.Read(first, parentLink);

            Assert.That(parent, Is.InstanceOf<IRealmObjectBase>());
            Assert.That(((IRealmObjectBase)parent!).DynamicApi.Get<Guid>("ID"), Is.EqualTo(set.DynamicApi.Get<Guid>("ID")));
        }

        private static void captureAll(Realms.Realm realm, RealmSchemaSnapshot schema, Dictionary<string, string> sink)
        {
            foreach (var classSchema in schema.Classes.Where(c => !c.IsEmbedded))
            {
                int taken = 0;

                foreach (var row in DynamicRealmAccess.All(realm, classSchema.Name))
                {
                    sink[describe(row, classSchema, schema)] = canonicalRow(row, classSchema, schema);

                    if (++taken >= max_rows_per_class)
                        break;
                }
            }
        }

        private static void rewriteAll(Realms.Realm realm, RealmSchemaSnapshot schema)
        {
            foreach (var classSchema in schema.Classes.Where(c => !c.IsEmbedded))
            {
                int taken = 0;

                foreach (var row in DynamicRealmAccess.All(realm, classSchema.Name))
                {
                    foreach (var property in classSchema.Properties.Where(p => isWritable(schema, p)))
                        DynamicValueCodec.Write(row, property, DynamicValueCodec.Read(row, property));

                    if (++taken >= max_rows_per_class)
                        break;
                }
            }
        }

        /// <summary>
        /// 可写列：标量与标量集合；链接只写目标为非嵌入类的（嵌入式对象不能重挂到同一父级，
        /// 重建它属于复制语义，不属于引擎语义）。
        /// </summary>
        private static bool isWritable(RealmSchemaSnapshot schema, RealmPropertySchema property)
        {
            if (property.ElementType == PropertyType.LinkingObjects)
                return false;

            if (property.ElementType == PropertyType.Object)
                return !property.IsDictionary && schema.Find(property.ObjectType) is { IsEmbedded: false };

            return true;
        }

        private static string canonicalRow(IRealmObjectBase row, RealmClassSchema classSchema, RealmSchemaSnapshot schema) =>
            string.Join(
                "|",
                classSchema.PropertyNames.Select(name =>
                {
                    RealmPropertySchema property = classSchema.Find(name)!;
                    return name + "=" + canonical(DynamicValueCodec.Read(row, property), schema);
                }));

        private static string canonical(object? value, RealmSchemaSnapshot? schema)
        {
            switch (value)
            {
                case null:
                    return "null";

                case RealmValue realmValue:
                    return $"rv:{realmValue.Type}:{canonical(realmValue.Type == RealmValueType.Null ? null : realmValue.AsAny(), schema)}";

                case byte[] bytes:
                    return "bytes:" + Convert.ToBase64String(bytes);

                case string text:
                    return "str:" + text;

                case float floatNumber:
                    return "f32:" + floatNumber.ToString("R", CultureInfo.InvariantCulture);

                case double doubleNumber:
                    return "f64:" + doubleNumber.ToString("R", CultureInfo.InvariantCulture);

                case DateTimeOffset date:
                    return "date:" + date.ToString("O", CultureInfo.InvariantCulture);

                case IRealmObjectBase realmObject when schema != null:
                    return canonicalObject(realmObject, schema);

                case IReadOnlyDictionary<string, object?> dictionary:
                    return "{" + string.Join(",", dictionary.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + canonical(p.Value, schema))) + "}";

                case IEnumerable sequence:
                    return "[" + string.Join(",", sequence.Cast<object?>().Select(item => canonical(item, schema))) + "]";

                default:
                    return value.GetType().Name + ":" + Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        private static string canonicalObject(IRealmObjectBase obj, RealmSchemaSnapshot schema)
        {
            string className = obj.ObjectSchema?.Name ?? "?";

            if (!schema.TryFindClass(className, out var classSchema))
                return className;

            // 嵌入对象没有主键：不展开列就看不出"链接还在不在"。
            if (classSchema.IsEmbedded)
            {
                return className + "{" + string.Join(
                    ",",
                    classSchema.PropertyNames
                        .Where(name => classSchema.Find(name)!.ElementType != PropertyType.LinkingObjects)
                        .Select(name => name + "=" + canonical(DynamicValueCodec.Read(obj, classSchema.Find(name)!), schema))) + "}";
            }

            if (classSchema.PrimaryKeyProperty is { } primaryKey && classSchema.Find(primaryKey) is { } primaryKeyProperty)
                return className + "#" + canonical(DynamicValueCodec.Read(obj, primaryKeyProperty), schema);

            return className;
        }

        private static string describe(IRealmObjectBase row, RealmClassSchema classSchema, RealmSchemaSnapshot schema)
        {
            if (classSchema.PrimaryKeyProperty is { } primaryKey && classSchema.Find(primaryKey) is { } primaryKeyProperty)
                return $"{classSchema.Name}#{canonical(DynamicValueCodec.Read(row, primaryKeyProperty), schema)}";

            return $"{classSchema.Name}@{row.GetHashCode()}";
        }

        private static IRealmObjectBase? firstRow(Realms.Realm realm, string className)
        {
            foreach (var row in DynamicRealmAccess.All(realm, className))
                return row;

            return null;
        }

        private static string copyToTemp(string source)
        {
            string root = Path.Combine(Path.GetTempPath(), "EzRealmSyncCodec", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            string target = Path.Combine(root, Path.GetFileName(source));
            File.Copy(source, target, overwrite: true);
            return target;
        }

        private static DynamicRealmSession openReadOnly(string path, out int version)
        {
            DynamicRealmSession session = DynamicRealmSession.OpenDynamic(path, readOnly: true);
            version = session.DiskSchemaVersion;
            return session;
        }

        private static string? tryResolveOfficialSample()
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestResources", "RealmSamples", "official", "client.realm");
            return File.Exists(path) ? path : null;
        }

        private static void tryDelete(string root)
        {
            try
            {
                string? directory = Path.GetDirectoryName(root);
                if (directory != null && Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
            catch
            {
                // 清理失败不影响断言结果。
            }
        }
    }
}
