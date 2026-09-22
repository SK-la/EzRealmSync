using System.Collections;
using System.Globalization;
using System.Reflection;
using MongoDB.Bson;
using Realms;
using Realms.Schema;

namespace osu.Game.EzRealmSync.Realm.Dynamic
{
    /// <summary>
    /// 按 schema 的列类型分派的通用动态读写。
    ///
    /// 泛型参数必须由 schema 决定，不能按运行时值推断：<c>DynamicApi.GetList&lt;T&gt;</c> 不校验元素类型，
    /// 元素转换发生在枚举/索引时（见 <see cref="DynamicRealmAccess"/> 的注释），猜错要到那时才炸。
    ///
    /// 读出的标量按列类型归一：整型一律 <see cref="long"/>，浮点按列类型区分 float / double ——
    /// <c>RealmValue.AsAny()</c> 会把 float 也归一成 double，直接写回会改掉列语义。
    /// 混合列（<see cref="PropertyType.RealmValue"/>）原样返回 <see cref="RealmValue"/>，不做归一，
    /// 否则 int 也会被归一成 long 而改掉实际存储的类型。
    /// </summary>
    public static class DynamicValueCodec
    {
        /// <summary>
        /// 读一列。集合列返回 <c>IReadOnlyList&lt;object?&gt;</c>（字典列返回
        /// <c>IReadOnlyDictionary&lt;string, object?&gt;</c>），链接列返回 <see cref="IRealmObjectBase"/>，
        /// 反向链接列返回 <c>IReadOnlyList&lt;IRealmObjectBase&gt;</c>，标量按列类型归一。
        /// </summary>
        public static object? Read(IRealmObjectBase obj, RealmPropertySchema property)
        {
            ArgumentNullException.ThrowIfNull(obj);
            ArgumentNullException.ThrowIfNull(property);

            if (property.IsCollection)
                return readCollection(obj, property);

            return property.ElementType switch
            {
                PropertyType.Object => obj.DynamicApi.Get<IRealmObjectBase>(property.Name),
                PropertyType.LinkingObjects => obj.DynamicApi.GetBacklinks(property.Name).Cast<IRealmObjectBase>().ToList(),
                _ => readScalar(obj, property),
            };
        }

        /// <summary>
        /// 写一列。<paramref name="value"/> 用 <see cref="Read"/> 的输出形态即可（标量允许宽类型，
        /// 会按列类型收窄）。反向链接列不可写。
        /// </summary>
        public static void Write(IRealmObjectBase obj, RealmPropertySchema property, object? value)
        {
            ArgumentNullException.ThrowIfNull(obj);
            ArgumentNullException.ThrowIfNull(property);

            if (property.IsCollection)
            {
                writeCollection(obj, property, value);
                return;
            }

            switch (property.ElementType)
            {
                case PropertyType.Object:
                    obj.DynamicApi.Set(property.Name, toObjectValue(value));
                    return;

                case PropertyType.LinkingObjects:
                    throw new InvalidOperationException($"反向链接列 {property.Name} 不可写。");

                default:
                    obj.DynamicApi.Set(property.Name, toScalarValue(property.ElementType, value));
                    return;
            }
        }

        /// <summary>把标量值按列类型收窄到该列实际存储的 CLR 类型（整型 → long，浮点按列类型）。</summary>
        public static object? Coerce(object? value, PropertyType elementType)
        {
            if (value == null)
                return null;

            if (value is RealmValue realmValue)
                value = realmValue.Type == RealmValueType.Null ? null : realmValue.AsAny();

            if (value == null)
                return null;

            return elementType switch
            {
                PropertyType.Int => Convert.ToInt64(value, CultureInfo.InvariantCulture),
                PropertyType.Bool => (bool)value,
                PropertyType.String => (string)value,
                PropertyType.Data => (byte[])value,
                PropertyType.Date => (DateTimeOffset)value,
                PropertyType.Float => Convert.ToSingle(value, CultureInfo.InvariantCulture),
                PropertyType.Double => Convert.ToDouble(value, CultureInfo.InvariantCulture),
                PropertyType.Guid => (Guid)value,
                // Decimal / ObjectId 在 Realm 里分别是 Decimal128 与 Bson ObjectId，
                // 读出来就是该运行时类型，这里不再转换（数值转换会把 Decimal128 压成 decimal 再回不去）。
                _ => value,
            };
        }

        private static object? readScalar(IRealmObjectBase obj, RealmPropertySchema property)
        {
            RealmValue raw = obj.DynamicApi.Get<RealmValue>(property.Name);

            if (raw.Type == RealmValueType.Null)
                return null;

            return property.ElementType == PropertyType.RealmValue ? raw : asTyped(raw, property.ElementType);
        }

        private static object? asTyped(RealmValue value, PropertyType elementType) => elementType switch
        {
            PropertyType.Int => value.As<long>(),
            PropertyType.Bool => value.As<bool>(),
            PropertyType.String => value.As<string>(),
            PropertyType.Data => value.As<byte[]>(),
            PropertyType.Date => value.As<DateTimeOffset>(),
            PropertyType.Float => value.As<float>(),
            PropertyType.Double => value.As<double>(),
            PropertyType.Decimal => value.AsDecimal128(),
            PropertyType.ObjectId => value.AsObjectId(),
            PropertyType.Guid => value.As<Guid>(),
            _ => value.AsAny(),
        };

        private static object? readCollection(IRealmObjectBase obj, RealmPropertySchema property)
        {
            Type elementClrType = clrTypeFor(property.ElementType);

            if (property.IsDictionary)
                return invoke(read_dictionary_method, elementClrType, obj, property.Name);

            if (property.IsSet)
                return invoke(read_set_method, elementClrType, obj, property.Name);

            return invoke(read_list_method, elementClrType, obj, property.Name);
        }

        private static void writeCollection(IRealmObjectBase obj, RealmPropertySchema property, object? value)
        {
            if (property.IsDictionary)
            {
                writeDictionaryValue(obj, property, value);
                return;
            }

            object?[] items = flatten(value, property);
            Type elementClrType = clrTypeFor(property.ElementType);

            if (property.IsSet)
                invoke(write_set_method, elementClrType, obj, property.Name, property.ElementType, items);
            else
                invoke(write_list_method, elementClrType, obj, property.Name, property.ElementType, items);
        }

        private static object?[] flatten(object? value, RealmPropertySchema property)
        {
            if (value == null)
                return [];

            if (value is IEnumerable enumerable and not string)
                return enumerable.Cast<object?>().ToArray();

            throw new InvalidOperationException($"列 {property.Name} 是集合，收到 {value.GetType().Name}。");
        }

        private static void writeDictionaryValue(IRealmObjectBase obj, RealmPropertySchema property, object? value)
        {
            IEnumerable<KeyValuePair<string, object?>> pairs;

            if (value == null)
                pairs = [];
            else if (value is IEnumerable<KeyValuePair<string, object?>> dictionary)
                pairs = dictionary;
            else
                throw new InvalidOperationException($"字典列 {property.Name} 需要键值对集合，收到 {value.GetType().Name}。");

            invoke(write_dictionary_method, clrTypeFor(property.ElementType), obj, property.Name, property.ElementType, pairs.ToArray());
        }

        private static RealmValue toObjectValue(object? value) => value switch
        {
            null => RealmValue.Null,
            RealmValue realmValue => realmValue,
            IRealmObjectBase realmObject => RealmValue.Object(realmObject),
            _ => throw new InvalidOperationException($"链接列只接受 Realm 对象，收到 {value.GetType().Name}。"),
        };

        private static RealmValue toScalarValue(PropertyType elementType, object? value)
        {
            if (value == null)
                return RealmValue.Null;

            if (value is RealmValue realmValue)
                return realmValue;

            return Coerce(value, elementType) switch
            {
                long number => number,
                bool flag => flag,
                string text => text,
                byte[] data => data,
                DateTimeOffset date => date,
                float floatNumber => floatNumber,
                double doubleNumber => doubleNumber,
                Decimal128 decimalNumber => decimalNumber,
                ObjectId objectId => objectId,
                Guid guid => guid,
                var other => throw new InvalidOperationException($"无法把 {other?.GetType().Name ?? "null"} 写入 {elementType} 列。"),
            };
        }

        private static Type clrTypeFor(PropertyType elementType) => elementType switch
        {
            PropertyType.Int => typeof(long),
            PropertyType.Bool => typeof(bool),
            PropertyType.String => typeof(string),
            PropertyType.Data => typeof(byte[]),
            PropertyType.Date => typeof(DateTimeOffset),
            PropertyType.Float => typeof(float),
            PropertyType.Double => typeof(double),
            PropertyType.Decimal => typeof(Decimal128),
            PropertyType.ObjectId => typeof(ObjectId),
            PropertyType.Guid => typeof(Guid),
            PropertyType.Object => typeof(IRealmObjectBase),
            _ => typeof(RealmValue),
        };

        private static IReadOnlyList<object?> readListCore<T>(IRealmObjectBase obj, string name) =>
            obj.DynamicApi.GetList<T>(name).Select(item => (object?)item).ToList();

        private static IReadOnlyList<object?> readSetCore<T>(IRealmObjectBase obj, string name) =>
            obj.DynamicApi.GetSet<T>(name).Select(item => (object?)item).ToList();

        private static IReadOnlyDictionary<string, object?> readDictionaryCore<T>(IRealmObjectBase obj, string name) =>
            obj.DynamicApi.GetDictionary<T>(name).ToDictionary(pair => pair.Key, pair => (object?)pair.Value, StringComparer.Ordinal);

        private static void writeListCore<T>(IRealmObjectBase obj, string name, PropertyType elementType, object?[] items)
        {
            IList<T> list = obj.DynamicApi.GetList<T>(name);
            list.Clear();

            foreach (object? item in items)
                list.Add((T)Coerce(item, elementType)!);
        }

        private static void writeSetCore<T>(IRealmObjectBase obj, string name, PropertyType elementType, object?[] items)
        {
            ISet<T> set = obj.DynamicApi.GetSet<T>(name);
            set.Clear();

            foreach (object? item in items)
                set.Add((T)Coerce(item, elementType)!);
        }

        private static void writeDictionaryCore<T>(IRealmObjectBase obj, string name, PropertyType elementType, KeyValuePair<string, object?>[] pairs)
        {
            IDictionary<string, T> dictionary = obj.DynamicApi.GetDictionary<T>(name);
            dictionary.Clear();

            foreach (var pair in pairs)
                dictionary[pair.Key] = (T)Coerce(pair.Value, elementType)!;
        }

        // 反射只在「构造闭合泛型方法」这一步用；闭合结果按 (方法, 元素类型) 缓存，逐行调用不重复反射。
        private static readonly MethodInfo read_list_method = typeof(DynamicValueCodec).GetMethod(nameof(readListCore), BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo read_set_method = typeof(DynamicValueCodec).GetMethod(nameof(readSetCore), BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo read_dictionary_method = typeof(DynamicValueCodec).GetMethod(nameof(readDictionaryCore), BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo write_list_method = typeof(DynamicValueCodec).GetMethod(nameof(writeListCore), BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo write_set_method = typeof(DynamicValueCodec).GetMethod(nameof(writeSetCore), BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo write_dictionary_method = typeof(DynamicValueCodec).GetMethod(nameof(writeDictionaryCore), BindingFlags.NonPublic | BindingFlags.Static)!;

        private static readonly Dictionary<(MethodInfo Definition, Type Element), MethodInfo> closed_generics = new();

        private static object? invoke(MethodInfo definition, Type elementClrType, params object?[] arguments) =>
            closeGeneric(definition, elementClrType).Invoke(null, arguments);

        private static MethodInfo closeGeneric(MethodInfo definition, Type elementClrType)
        {
            lock (closed_generics)
            {
                if (!closed_generics.TryGetValue((definition, elementClrType), out var closed))
                {
                    closed = definition.MakeGenericMethod(elementClrType);
                    closed_generics[(definition, elementClrType)] = closed;
                }

                return closed;
            }
        }
    }
}
