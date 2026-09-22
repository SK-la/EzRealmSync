using Realms;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm.Dynamic
{
    /// <summary>整表导出的范围限制：不给范围就读全库（真实样本可能很大）。</summary>
    public sealed record DynamicDumpOptions
    {
        /// <summary>只读这些类；null 表示读全部类。</summary>
        public IReadOnlyList<string>? ClassNames { get; init; }

        /// <summary>每类最多读多少行；null 表示不限。行序稳定，限行不影响可复现性。</summary>
        public int? MaxRowsPerClass { get; init; }

        public static readonly DynamicDumpOptions All = new DynamicDumpOptions();
    }

    public sealed record DynamicDumpClass(
        string Name,
        bool IsEmbedded,
        long RowCount,
        IReadOnlyList<string> Columns,
        IReadOnlyList<DynamicDumpRow> Rows,
        string? Note);

    public sealed record DynamicDumpRow(string Key, IReadOnlyDictionary<string, string> Cells);

    public sealed record DynamicRealmDump(
        int DiskSchemaVersion,
        string? SourcePath,
        IReadOnlyList<DynamicDumpClass> Classes)
    {
        public DynamicDumpClass? Find(string className) =>
            Classes.FirstOrDefault(c => string.Equals(c.Name, className, StringComparison.Ordinal));

        /// <summary>
        /// 与另一份导出逐类逐列逐行比对，返回差异描述；空列表表示完全一致。
        /// 行按 <see cref="DynamicDumpRow.Key"/> 配对，因此两份导出的行序不同不算差异。
        /// </summary>
        public IReadOnlyList<string> FindDifferences(DynamicRealmDump other, int maxReport = 50)
        {
            ArgumentNullException.ThrowIfNull(other);

            var differences = new List<string>();

            foreach (string className in Classes.Select(c => c.Name)
                                                .Union(other.Classes.Select(c => c.Name), StringComparer.Ordinal)
                                                .OrderBy(n => n, StringComparer.Ordinal))
            {
                if (differences.Count >= maxReport)
                    break;

                DynamicDumpClass? left = Find(className);
                DynamicDumpClass? right = other.Find(className);

                if (left == null)
                {
                    differences.Add($"类 {className}：另一份有、本份没有");
                    continue;
                }

                if (right == null)
                {
                    differences.Add($"类 {className}：本份有、另一份没有");
                    continue;
                }

                foreach (string column in left.Columns.Union(right.Columns, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal))
                {
                    if (left.Columns.Contains(column, StringComparer.Ordinal) && !right.Columns.Contains(column, StringComparer.Ordinal))
                        differences.Add($"{className}.{column}：另一份没有这一列");
                    else if (!left.Columns.Contains(column, StringComparer.Ordinal))
                        differences.Add($"{className}.{column}：本份没有这一列");
                }

                foreach (string key in left.Rows.Select(r => r.Key).Union(right.Rows.Select(r => r.Key), StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
                {
                    if (differences.Count >= maxReport)
                        break;

                    DynamicDumpRow? leftRow = left.Rows.FirstOrDefault(r => string.Equals(r.Key, key, StringComparison.Ordinal));
                    DynamicDumpRow? rightRow = right.Rows.FirstOrDefault(r => string.Equals(r.Key, key, StringComparison.Ordinal));

                    if (leftRow == null)
                    {
                        differences.Add($"{className}[{key}]：另一份有、本份没有");
                        continue;
                    }

                    if (rightRow == null)
                    {
                        differences.Add($"{className}[{key}]：本份有、另一份没有");
                        continue;
                    }

                    foreach (string column in leftRow.Cells.Keys.Union(rightRow.Cells.Keys, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal))
                    {
                        if (differences.Count >= maxReport)
                            break;

                        leftRow.Cells.TryGetValue(column, out string? leftValue);
                        rightRow.Cells.TryGetValue(column, out string? rightValue);

                        if (!string.Equals(leftValue ?? DynamicDumpValue.Null, rightValue ?? DynamicDumpValue.Null, StringComparison.Ordinal))
                            differences.Add($"{className}[{key}].{column}：{leftValue ?? DynamicDumpValue.Null} != {rightValue ?? DynamicDumpValue.Null}");
                    }
                }
            }

            return differences;
        }
    }

    /// <summary>
    /// 「把一份库整表整列读出来」的通用原语：类 → 列 → 行 → 单元格文本。
    ///
    /// 它服务**对照**：同一份库分别用 typed 模型与动态读出来，逐单元格比对（归一规则见
    /// <see cref="DynamicDumpValue"/>）。读的是磁盘 schema 里的每一列，不做任何基线白名单过滤——
    /// Ez 扩展列也要读，否则「同步没碰 Ez 列」这条就无从对照。
    ///
    /// 只读：不写、不迁移、不建表。
    /// </summary>
    public static class DynamicRealmDumper
    {
        public static DynamicRealmDump Dump(DynamicRealmSession session, DynamicDumpOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(session);

            options ??= DynamicDumpOptions.All;

            RealmSchemaSnapshot schema = DynamicSchemaReader.Read(session);
            var classes = new List<DynamicDumpClass>();

            foreach (RealmClassSchema classSchema in schema.Classes)
            {
                if (options.ClassNames != null && !options.ClassNames.Contains(classSchema.Name, StringComparer.Ordinal))
                    continue;

                classes.Add(dumpClass(session.Realm, classSchema, options));
            }

            return new DynamicRealmDump(session.DiskSchemaVersion, session.FilePath, classes);
        }

        private static DynamicDumpClass dumpClass(RealmInstance realm, RealmClassSchema classSchema, DynamicDumpOptions options)
        {
            var rows = new List<DynamicDumpRow>();
            long rowCount = 0;
            string? note = null;

            if (classSchema.IsEmbedded)
            {
                // 嵌入对象没有独立表，Realm 不允许直接查询。它们由持有者以「类#?」出现，
                // 因此这里给出的是「本类没有独立行」这个事实，而不是失败。
                return new DynamicDumpClass(classSchema.Name, true, 0, classSchema.PropertyNames, rows, "嵌入类不直接枚举");
            }

            try
            {
                IQueryable<IRealmObjectBase> all = realm.DynamicApi.All(classSchema.Name);
                int index = 0;

                foreach (IRealmObjectBase row in all)
                {
                    rowCount++;

                    if (options.MaxRowsPerClass is { } limit && rows.Count >= limit)
                        continue;

                    rows.Add(dumpRow(row, classSchema, index));
                    index++;
                }

                if (options.MaxRowsPerClass is { } max && rowCount > max)
                    note = $"只导出了前 {max} 行（共 {rowCount} 行）";
            }
            catch (Exception ex)
            {
                // 嵌入类不可直接枚举、个别类在异常 schema 上不可读：记成 0 行 + 说明，不打断整库导出。
                note = $"无法枚举：{ex.GetType().Name} {ex.Message}";
            }

            if (classSchema.PrimaryKeyProperty == null)
                rows = canonicalizeRows(rows);

            return new DynamicDumpClass(
                classSchema.Name,
                classSchema.IsEmbedded,
                rowCount,
                classSchema.PropertyNames,
                rows,
                note);
        }

        /// <summary>
        /// 无主键的类没有稳定行标识：按整行文本排序后重新编号。
        /// 排序后行序只取决于内容，因此两份导出的行集合可以直接按编号配对。
        /// </summary>
        private static List<DynamicDumpRow> canonicalizeRows(List<DynamicDumpRow> rows)
        {
            return rows
                   .OrderBy(r => rowText(r), StringComparer.Ordinal)
                   .Select((r, i) => r with { Key = $"#{i}" })
                   .ToList();
        }

        private static string rowText(DynamicDumpRow row) =>
            string.Join("\u0001", row.Cells.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => $"{c.Key}={c.Value}"));

        private static DynamicDumpRow dumpRow(IRealmObjectBase row, RealmClassSchema classSchema, int index)
        {
            var cells = new Dictionary<string, string>(StringComparer.Ordinal);
            string key = $"#{index}";

            foreach (RealmPropertySchema property in classSchema.Properties)
            {
                string text = readCell(row, property);

                if (property.IsPrimaryKey)
                    key = text;

                cells[property.Name] = text;
            }

            return new DynamicDumpRow(key, cells);
        }

        /// <summary>
        /// 读一个单元格。读失败时留下 <see cref="DynamicDumpValue.Unreadable"/>，而不是当成空值——
        /// 「读不出来」正是要抓的 bug，不能与「本来就是 null」混为一谈。
        /// </summary>
        private static string readCell(IRealmObjectBase row, RealmPropertySchema property)
        {
            try
            {
                return DynamicDumpValue.DescribeColumn(DynamicValueCodec.Read(row, property), property);
            }
            catch
            {
                return DynamicDumpValue.Unreadable;
            }
        }
    }
}
