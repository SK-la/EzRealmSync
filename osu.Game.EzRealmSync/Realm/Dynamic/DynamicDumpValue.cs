using System.Collections;
using System.Globalization;
using Realms;
using Realms.Schema;

namespace osu.Game.EzRealmSync.Realm.Dynamic
{
    /// <summary>
    /// 动态值与 typed 值的**同一套归一文本**：把任意列值压成一个稳定字符串，用于逐单元格比对。
    ///
    /// 归一必须只有一处定义：动态侧与 typed 侧都用它，比对才有意义。因此文本只求「稳定可辨」，
    /// 不求人读友好（日期用 O 格式、浮点用 R 格式，避免因格式差异产生假不等）。
    ///
    /// 已知弱化：没有主键的链接对象（含嵌入对象）只描述成 <c>类#?</c>，其内部字段不参与比对。
    /// 官方基线里参与同步的类都有主键，这个弱化不影响「同步写入列值正确」这条的验证强度。
    /// </summary>
    public static class DynamicDumpValue
    {
        public const string Null = "<null>";

        /// <summary>读不出来的值：与真正的 null 区分，避免把「读失败」当成「字段为空」而漏掉 bug。</summary>
        public const string Unreadable = "<unreadable>";

        public static string Describe(object? value)
        {
            switch (value)
            {
                case null:
                    return Null;

                case string text:
                    return text;

                case bool flag:
                    return flag ? "true" : "false";

                case byte[] bytes:
                    // 内容不参与比对：Data 列在官方基线里只出现在图片/文件字节，长度足以发现截断。
                    return $"<bytes:{bytes.Length}>";

                case RealmValue realmValue:
                    return describeRealmValue(realmValue);

                case IRealmObjectBase realmObject:
                    return describeObject(realmObject);

                case DateTimeOffset date:
                    return date.ToString("O", CultureInfo.InvariantCulture);

                case Guid guid:
                    return guid.ToString("D", CultureInfo.InvariantCulture);

                case Enum enumValue:
                    return describeEnum(enumValue);

                case IFormattable formattable:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);

                case IEnumerable enumerable:
                    return describeEnumerable(enumerable);

                default:
                    return value.ToString() ?? Null;
            }
        }

        /// <summary>
        /// 列值 → 文本，附带该列的类型信息。
        /// typed 侧反射读出的值常常是「枚举但其实存成整数」这类，只有列 schema 才能归一到位。
        /// </summary>
        public static string DescribeColumn(object? value, RealmPropertySchema? property)
        {
            if (value == null)
                return Null;

            if (property is { IsCollection: false, IsDictionary: false })
            {
                switch (property.ElementType)
                {
                    case PropertyType.Int:
                    case PropertyType.Float:
                    case PropertyType.Double:
                        if (value is Enum integralEnum)
                            return Convert.ToInt64(integralEnum, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                        break;

                    case PropertyType.String:
                        if (value is Enum textEnum)
                            return textEnum.ToString();
                        break;

                    case PropertyType.Guid:
                        if (value is string guidText && Guid.TryParse(guidText, out Guid parsed))
                            return parsed.ToString("D", CultureInfo.InvariantCulture);
                        break;
                }
            }

            return Describe(value);
        }

        private static string describeRealmValue(RealmValue value)
        {
            if (value.Type == RealmValueType.Null)
                return Null;

            if (value.Type == RealmValueType.Object)
                return $"<rv:Object>{describeObject(value.AsObjectOrNull())}";

            return $"<rv:{value.Type}>{Describe(value.AsAny())}";
        }

        private static string describeObject(IRealmObjectBase? realmObject)
        {
            if (realmObject == null)
                return Null;

            try
            {
                ObjectSchema schema = realmObject.ObjectSchema;
                string? primaryKey = null;

                foreach (Property candidate in schema)
                {
                    if (candidate.IsPrimaryKey)
                    {
                        primaryKey = candidate.Name;
                        break;
                    }
                }

                if (primaryKey == null)
                    return $"{schema.Name}#?";

                RealmValue key = realmObject.DynamicApi.Get<RealmValue>(primaryKey);
                return $"{schema.Name}#{(key.Type == RealmValueType.Null ? "?" : Describe(key.AsAny()))}";
            }
            catch
            {
                return $"{realmObject.GetType().Name}#{Unreadable}";
            }
        }

        private static string describeEnumerable(IEnumerable enumerable)
        {
            var parts = new List<string>();
            bool dictionary = false;

            foreach (object? item in enumerable)
            {
                if (item == null)
                {
                    parts.Add(Null);
                    continue;
                }

                Type itemType = item.GetType();

                if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                {
                    dictionary = true;
                    object? key = itemType.GetProperty("Key")?.GetValue(item);
                    object? value = itemType.GetProperty("Value")?.GetValue(item);
                    parts.Add($"{Describe(key)}={Describe(value)}");
                    continue;
                }

                parts.Add(Describe(item));
            }

            // 字典按「键=值」排序后比对：键的枚举顺序不是契约。列表不排序，顺序本身是写入结果的一部分。
            if (dictionary)
                parts.Sort(StringComparer.Ordinal);

            return $"{(dictionary ? "{" : "[")}{string.Join(",", parts)}{(dictionary ? "}" : "]")}";
        }

        private static string describeEnum(Enum value)
        {
            // 未落在已声明成员上的值（Realm 存的是原始数值）按数值归一；
            // 其余按成员名归一，避免「动态读到名字、typed 读到数值」这种假不等。
            return Enum.IsDefined(value.GetType(), value)
                ? value.ToString()
                : Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType()), CultureInfo.InvariantCulture)?.ToString() ?? value.ToString();
        }
    }

    /// <summary><see cref="RealmValue"/> 里装的对象：类型不匹配时返回 null 而不是抛异常。</summary>
    internal static class RealmValueObjectAccess
    {
        public static IRealmObjectBase? AsObjectOrNull(this RealmValue value)
        {
            try
            {
                return value.As<IRealmObjectBase>();
            }
            catch
            {
                return null;
            }
        }
    }
}
