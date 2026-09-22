using System.Collections;
using System.Reflection;
using Realms;
using Realms.Schema;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>按磁盘 schema 列名读写动态对象；未知列 / Ez 列直接跳过。</summary>
    internal static class DynamicRealmAccess
    {
        public static T? Get<T>(IRealmObjectBase? obj, string property)
        {
            if (obj == null || !HasProperty(obj, property))
                return default;

            try
            {
                return obj.DynamicApi.Get<T>(property);
            }
            catch
            {
                return default;
            }
        }

        public static string GetString(IRealmObjectBase? obj, string property) =>
            Get<string>(obj, property) ?? string.Empty;

        /// <summary>
        /// 读原始值：不过白名单、不跳 Ez 列。**只**供「删行重建前暂存 Ez 列原值」用。
        /// </summary>
        public static RealmValue? GetRaw(IRealmObjectBase? obj, string property)
        {
            if (obj == null || !HasProperty(obj, property))
                return null;

            try
            {
                return obj.DynamicApi.Get<RealmValue>(property);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 写回原始值：绕过 Ez 列守卫。仅用于把同一行删除前的原值还原——这里的值**全部来自本库原有行**，
        /// 不是同步产物，所以不违反「同步不产生 Ez 列新值」。
        /// </summary>
        public static void SetRaw(IRealmObjectBase obj, string property, RealmValue value)
        {
            if (!HasProperty(obj, property))
                return;

            obj.DynamicApi.Set(property, value);
        }

        public static void Set(IRealmObjectBase obj, string className, string property, object? value)
        {
            if (OfficialBaselineSchema.EzOnlyPropertyNames.Contains(property, StringComparer.Ordinal))
                return;

            if (!OfficialBaselineSchema.IsKnownProperty(className, property))
                return;

            if (!HasProperty(obj, property))
                return;

            if (value is null)
            {
                obj.DynamicApi.Set(property, RealmValue.Null);
                return;
            }

            if (value is IRealmObjectBase)
            {
                setObject(obj, property, value);
                return;
            }

            if (value is string text)
            {
                obj.DynamicApi.Set(property, text);
                return;
            }

            if (value is bool flag)
            {
                obj.DynamicApi.Set(property, flag);
                return;
            }

            if (value is int number)
            {
                obj.DynamicApi.Set(property, number);
                return;
            }

            if (value is long longNumber)
            {
                obj.DynamicApi.Set(property, longNumber);
                return;
            }

            if (value is float floatNumber)
            {
                obj.DynamicApi.Set(property, floatNumber);
                return;
            }

            if (value is double doubleNumber)
            {
                obj.DynamicApi.Set(property, doubleNumber);
                return;
            }

            if (value is Guid guid)
            {
                obj.DynamicApi.Set(property, guid);
                return;
            }

            if (value is DateTimeOffset date)
            {
                obj.DynamicApi.Set(property, date);
                return;
            }

            Type type = value.GetType();
            if (type == typeof(DateTimeOffset))
            {
                obj.DynamicApi.Set(property, (DateTimeOffset)value);
                return;
            }

            if (type == typeof(double))
            {
                obj.DynamicApi.Set(property, (double)value);
                return;
            }

            if (type == typeof(int))
            {
                obj.DynamicApi.Set(property, (int)value);
                return;
            }

            throw new InvalidOperationException($"无法写入动态属性 {className}.{property}（{type.Name}）。");
        }

        private static void setObject(IRealmObjectBase obj, string property, object value)
        {
            RealmValue boxed = boxRealmObject(value);
            obj.DynamicApi.Set(property, boxed);
        }

        private static RealmValue boxRealmObject(object value)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            Type realmValueType = typeof(RealmValue);

            foreach (var ctor in realmValueType.GetConstructors(flags))
            {
                var parameters = ctor.GetParameters();
                if (parameters.Length != 1)
                    continue;

                if (!parameters[0].ParameterType.IsInstanceOfType(value)
                    && parameters[0].ParameterType != typeof(IRealmObjectBase)
                    && parameters[0].ParameterType != typeof(object))
                {
                    continue;
                }

                try
                {
                    return (RealmValue)ctor.Invoke([value]);
                }
                catch
                {
                    // 继续尝试其它构造。
                }
            }

            foreach (var method in realmValueType.GetMethods(flags))
            {
                if (method.ReturnType != typeof(RealmValue) || method.GetParameters().Length != 1)
                    continue;

                Type parameterType = method.GetParameters()[0].ParameterType;
                if (!parameterType.IsInstanceOfType(value)
                    && parameterType != typeof(IRealmObjectBase)
                    && parameterType != typeof(object))
                {
                    continue;
                }

                try
                {
                    object? created = method.IsStatic ? method.Invoke(null, [value]) : null;
                    if (created is RealmValue realmValue)
                        return realmValue;
                }
                catch
                {
                    // 继续尝试其它工厂。
                }
            }

            throw new InvalidOperationException($"无法把动态对象装箱为 RealmValue（{value.GetType().FullName}）。");
        }

        public static bool HasProperty(IRealmObjectBase obj, string property) => findProperty(obj, property) != null;

        /// <summary>
        /// 取属性 schema。<c>DynamicApi.GetList&lt;T&gt;</c> 不会按 schema 校验元素类型，
        /// 元素转换发生在枚举/索引时，因此必须先用 <see cref="Property.ObjectType"/> 决定 T。
        /// </summary>
        private static Property? findProperty(IRealmObjectBase obj, string property)
        {
            try
            {
                // 要取到局部：连写两次 obj.ObjectSchema 时编译器无法证明第二次非空（CS8602）。
                if (obj.ObjectSchema is { } schema)
                {
                    foreach (var candidate in schema)
                    {
                        if (string.Equals(candidate.Name, property, StringComparison.Ordinal))
                            return candidate;
                    }
                }
            }
            catch
            {
                // 对象已失效（句柄被回收 / 已从 Realm 移除）。
            }

            return null;
        }

        /// <summary>
        /// 取得动态列表句柄：对象列表（含 embedded）返回 <c>IList&lt;IRealmObjectBase&gt;</c>
        /// （可直接交给 <c>AddEmbeddedObjectToList</c>），值列表返回 <c>IList&lt;RealmValue&gt;</c>。
        /// <c>Dynamic.Get</c> 对列表属性一律抛 NotSupportedException。
        /// </summary>
        public static object? GetListRaw(IRealmObjectBase obj, string property)
        {
            if (findProperty(obj, property) is not { } schema || (schema.Type & PropertyType.Array) == 0)
                return null;

            try
            {
                // RealmValue 不能承载 embedded object，所以对象列表必须用 IRealmObjectBase。
                return string.IsNullOrEmpty(schema.ObjectType)
                    ? obj.DynamicApi.GetList<RealmValue>(property)
                    : obj.DynamicApi.GetList<IRealmObjectBase>(property);
            }
            catch
            {
                return null;
            }
        }

        public static IEnumerable<IRealmObjectBase> EnumerateObjects(IRealmObjectBase obj, string property)
        {
            object? raw = GetListRaw(obj, property);
            if (raw == null)
                yield break;

            if (raw is IRealmObjectBase single)
            {
                yield return single;
                yield break;
            }

            if (raw is not IEnumerable enumerable || raw is string)
                yield break;

            foreach (object? item in enumerable)
            {
                object? value = unwrap(item);

                if (value is IRealmObjectBase realmObject)
                    yield return realmObject;
            }
        }

        public static IEnumerable<T> EnumerateValues<T>(IRealmObjectBase obj, string property)
        {
            object? raw = GetListRaw(obj, property);
            if (raw is not IEnumerable enumerable || raw is string)
                yield break;

            foreach (object? item in enumerable)
            {
                if (item is RealmValue realmValue)
                {
                    if (realmValue.Type == RealmValueType.Null)
                        continue;

                    // RealmValue.AsAny 会把所有整数归一成 long、浮点归一成 double，
                    // 因此 IList<int> / IList<float> 这类窄类型必须走 As<T> 的数值转换。
                    if (tryAsValue<T>(realmValue, out T? converted))
                        yield return converted!;

                    continue;
                }

                if (item is T typed)
                    yield return typed;
            }
        }

        private static bool tryAsValue<T>(RealmValue value, out T? result)
        {
            try
            {
                result = value.As<T>();
                return true;
            }
            catch
            {
                // 数值溢出 / 类型无转换路径：按「该元素不可用」跳过，不影响同列表其它元素。
                result = default;
                return false;
            }
        }

        private static object? unwrap(object? item) =>
            item is RealmValue realmValue
                ? realmValue.Type == RealmValueType.Null ? null : realmValue.AsAny()
                : item;

        public static void ClearList(object? list)
        {
            if (list is IList generic)
            {
                generic.Clear();
                return;
            }

            list?.GetType().GetMethod("Clear", Type.EmptyTypes)?.Invoke(list, null);
        }

        /// <summary>
        /// 只走 <c>IList&lt;T&gt;</c> 泛型接口：Realm 20.1.0 的 <c>RealmCollectionBase&lt;T&gt;.Add(object)</c>
        /// 会自我递归（栈溢出），因此禁止使用非泛型 <c>IList.Add</c>。
        /// </summary>
        public static void AddToList(object? list, object? item)
        {
            if (list == null || item == null)
                return;

            if (list is IList<RealmValue> realmValues)
            {
                realmValues.Add(toRealmValue(item));
                return;
            }

            if (list is IList<IRealmObjectBase> realmObjects)
            {
                if (item is IRealmObjectBase realmObject)
                    realmObjects.Add(realmObject);

                return;
            }

            throw new InvalidOperationException($"不支持的列表类型（{list.GetType().Name}）。");
        }

        private static RealmValue toRealmValue(object value) => value switch
        {
            RealmValue realmValue => realmValue,
            string text => text,
            bool flag => flag,
            int number => number,
            long longNumber => longNumber,
            float floatNumber => floatNumber,
            double doubleNumber => doubleNumber,
            Guid guid => guid,
            DateTimeOffset date => date,
            _ => throw new InvalidOperationException($"无法把 {value.GetType().Name} 加入 RealmValue 列表。"),
        };

        public static IRealmObjectBase Create(RealmInstance realm, string className) =>
            realm.DynamicApi.CreateObject(className);

        public static IRealmObjectBase Create(RealmInstance realm, string className, Guid primaryKey) =>
            realm.DynamicApi.CreateObject(className, primaryKey);

        public static IRealmObjectBase Create(RealmInstance realm, string className, string primaryKey) =>
            realm.DynamicApi.CreateObject(className, primaryKey);

        public static IRealmObjectBase? Find(RealmInstance realm, string className, Guid id) =>
            realm.DynamicApi.Find(className, id);

        public static IRealmObjectBase? Find(RealmInstance realm, string className, string primaryKey) =>
            realm.DynamicApi.Find(className, primaryKey);

        public static IQueryable<IRealmObjectBase> All(RealmInstance realm, string className) =>
            realm.DynamicApi.All(className);
    }
}
