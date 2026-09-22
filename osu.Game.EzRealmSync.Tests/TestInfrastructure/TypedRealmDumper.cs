#if HAS_EZ_OSU_GAME
using System.Collections;
using System.Reflection;
using osu.Game.EzRealmSync.Realm.Dynamic;
using Realms;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Tests.TestInfrastructure
{
    /// <summary>
    /// 一次 typed 导出，外加「模型覆盖不到的地方」两笔账：
    /// <see cref="ClassesWithoutModel"/>（磁盘上有、模型里没有的类）与
    /// <see cref="ColumnsWithoutModel"/>（磁盘上有、模型里没有的列）。
    ///
    /// 这两笔账必须显式核对：模型少一个类，parity 就静默少比一整张表。
    /// </summary>
    internal sealed record TypedDumpResult(
        DynamicRealmDump Dump,
        IReadOnlyList<string> ClassesWithoutModel,
        IReadOnlyList<string> ColumnsWithoutModel);

    /// <summary>
    /// typed 侧的整表整列导出：用 <c>osu.Game</c> 的 Realm 模型 + 反射读同一份库。
    ///
    /// 与动态侧共用的只有「值 → 文本」这一步（<see cref="DynamicDumpValue"/>）：两边归一规则
    /// 不同的话，比对结果就没有意义。行的标识、列的命名也按同一套规则（<c>MapTo</c> 之后的磁盘列名）。
    ///
    /// 嵌入类不直接枚举（Realm 的 <c>All&lt;T&gt;</c> 不支持），由持有它的对象以 <c>类#?</c> 出现。
    /// </summary>
    internal static class TypedRealmDumper
    {
        private static readonly IReadOnlyDictionary<string, Type> model_types = buildModelTypes();

        private static readonly MethodInfo all_method = typeof(RealmInstance)
                                                        .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                                        .First(m => m.Name == nameof(RealmInstance.All) && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);

        public static TypedDumpResult Dump(
            RealmInstance realm,
            RealmSchemaSnapshot schema,
            int diskSchemaVersion,
            IReadOnlyList<string>? classNames = null,
            int? maxRowsPerClass = null)
        {
            ArgumentNullException.ThrowIfNull(realm);
            ArgumentNullException.ThrowIfNull(schema);

            var classes = new List<DynamicDumpClass>();
            var classesWithoutModel = new List<string>();
            var columnsWithoutModel = new List<string>();

            foreach (RealmClassSchema classSchema in schema.Classes)
            {
                if (classNames != null && !classNames.Contains(classSchema.Name, StringComparer.Ordinal))
                    continue;

                if (classSchema.IsEmbedded)
                {
                    classes.Add(new DynamicDumpClass(classSchema.Name, true, 0, classSchema.PropertyNames, [], "嵌入类不直接枚举"));
                    continue;
                }

                if (!model_types.TryGetValue(classSchema.Name, out Type? modelType))
                {
                    classesWithoutModel.Add(classSchema.Name);
                    classes.Add(new DynamicDumpClass(classSchema.Name, false, 0, classSchema.PropertyNames, [], "模型里没有这个类"));
                    continue;
                }

                var mapped = mapProperties(modelType, classSchema);

                foreach (string missing in classSchema.PropertyNames.Where(name => mapped.All(m => m.Column != name)).OrderBy(n => n, StringComparer.Ordinal))
                    columnsWithoutModel.Add($"{classSchema.Name}.{missing}");

                classes.Add(dumpClass(realm, classSchema, modelType, mapped, maxRowsPerClass));
            }

            return new TypedDumpResult(
                new DynamicRealmDump(diskSchemaVersion, null, classes),
                classesWithoutModel,
                columnsWithoutModel);
        }

        private static DynamicDumpClass dumpClass(
            RealmInstance realm,
            RealmClassSchema classSchema,
            Type modelType,
            IReadOnlyList<MappedProperty> mapped,
            int? maxRowsPerClass)
        {
            var rows = new List<DynamicDumpRow>();
            long rowCount = 0;
            string? note = null;

            try
            {
                object? query = all_method.MakeGenericMethod(modelType).Invoke(realm, null);

                if (query is not IEnumerable enumerable)
                {
                    note = "模型类型无法枚举（All<T> 返回了非集合）";
                    return new DynamicDumpClass(classSchema.Name, false, 0, classSchema.PropertyNames, rows, note);
                }

                int index = 0;

                foreach (object? row in enumerable)
                {
                    if (row == null)
                        continue;

                    rowCount++;

                    if (maxRowsPerClass is { } limit && rows.Count >= limit)
                        continue;

                    rows.Add(dumpRow(row, classSchema, mapped, index));
                    index++;
                }
            }
            catch (Exception ex)
            {
                note = $"无法枚举：{ex.GetType().Name} {ex.Message}";
            }

            if (classSchema.PrimaryKeyProperty == null)
                rows = rows.OrderBy(rowText, StringComparer.Ordinal).Select((r, i) => r with { Key = $"#{i}" }).ToList();

            return new DynamicDumpClass(classSchema.Name, false, rowCount, classSchema.PropertyNames, rows, note);
        }

        private static DynamicDumpRow dumpRow(object row, RealmClassSchema classSchema, IReadOnlyList<MappedProperty> mapped, int index)
        {
            var cells = new Dictionary<string, string>(StringComparer.Ordinal);
            string key = $"#{index}";

            foreach (MappedProperty property in mapped)
            {
                string text = readCell(row, property);

                if (property.IsPrimaryKey)
                    key = text;

                cells[property.Column] = text;
            }

            return new DynamicDumpRow(key, cells);
        }

        private static string readCell(object row, MappedProperty property)
        {
            try
            {
                object? value = property.Property.GetValue(row);
                return DynamicDumpValue.DescribeColumn(value, property.Schema);
            }
            catch
            {
                return DynamicDumpValue.Unreadable;
            }
        }

        private static string rowText(DynamicDumpRow row) =>
            string.Join("\u0001", row.Cells.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => $"{c.Key}={c.Value}"));

        /// <summary>模型属性 → 磁盘列名；模型没存到磁盘的属性（Ignored / 计算属性）不参与。</summary>
        private static IReadOnlyList<MappedProperty> mapProperties(Type modelType, RealmClassSchema classSchema)
        {
            var mapped = new List<MappedProperty>();

            foreach (PropertyInfo property in modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetCustomAttribute<IgnoredAttribute>() != null)
                    continue;

                if (property.GetIndexParameters().Length != 0)
                    continue;

                string column = property.GetCustomAttribute<MapToAttribute>()?.Mapping ?? property.Name;

                if (!classSchema.TryFindProperty(column, out RealmPropertySchema? schema))
                    continue;

                mapped.Add(new MappedProperty(property, column, schema, schema.IsPrimaryKey));
            }

            return mapped;
        }

        private static IReadOnlyDictionary<string, Type> buildModelTypes()
        {
            var map = new Dictionary<string, Type>(StringComparer.Ordinal);

            foreach (Type type in typeof(Beatmaps.BeatmapSetInfo).Assembly.GetTypes())
            {
                if (type.IsAbstract || !typeof(RealmObject).IsAssignableFrom(type))
                    continue;

                string name = type.GetCustomAttribute<MapToAttribute>()?.Mapping ?? type.Name;
                map[name] = type;
            }

            return map;
        }

        private sealed record MappedProperty(PropertyInfo Property, string Column, RealmPropertySchema Schema, bool IsPrimaryKey);
    }
}
#endif
