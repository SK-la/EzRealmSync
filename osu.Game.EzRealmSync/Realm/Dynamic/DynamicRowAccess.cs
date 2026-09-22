using Realms;

namespace osu.Game.EzRealmSync.Realm.Dynamic
{
    /// <summary>
    /// 动态读一行的**共用入口**：按「列路径」取值 + 软删 / 隐藏过滤。
    ///
    /// 浏览、导出、修复三处都要做这两件事，规则必须只有一处定义——不然同一份库在不同页面上
    /// 「哪些行算存在」会各有各的说法。过滤规则对齐官方 <c>Live*</c> 查询（<c>RealmQueryHelpers</c>）。
    /// </summary>
    internal static class DynamicRowAccess
    {
        /// <summary>
        /// 按「列路径」取值：点号分段，每段是当前对象的列名，可以逐段穿过链接
        /// （<c>BeatmapSet.Hash</c>、<c>Metadata.Title</c>、<c>File.Hash</c>）。
        ///
        /// 路径解析不出来（列不存在、链接为空、当前对象不是对象）一律返回 null 而不是抛异常：
        /// 库的 schema 版本千差万别，少一列不该让整页失败。
        /// </summary>
        public static object? Resolve(object? current, RealmSchemaSnapshot schema, string path)
        {
            foreach (string segment in path.Split('.'))
            {
                if (current is not IRealmObjectBase realmObject || realmObject.ObjectSchema is not { } objectSchema)
                    return null;

                if (!schema.TryFindClass(objectSchema.Name, out RealmClassSchema? classSchema))
                    return null;

                if (!classSchema.TryFindProperty(segment, out RealmPropertySchema? property))
                    return null;

                current = DynamicValueCodec.Read(realmObject, property);
            }

            return current;
        }

        public static string? ResolveString(IRealmObjectBase row, RealmSchemaSnapshot schema, string path) =>
            Resolve(row, schema, path) as string;

        public static bool ResolveBool(IRealmObjectBase row, RealmSchemaSnapshot schema, string path) =>
            Resolve(row, schema, path) is true;

        /// <summary>
        /// 读整型列。<see cref="DynamicValueCodec"/> 把所有整型列统一读成 <see cref="long"/>，
        /// 所以这里只收 <see cref="long"/>；直接 <c>as int?</c> 永远得到 null。
        /// </summary>
        public static long? ResolveLong(IRealmObjectBase row, RealmSchemaSnapshot schema, string path) =>
            Resolve(row, schema, path) as long?;

        public static DateTimeOffset? ResolveDate(IRealmObjectBase row, RealmSchemaSnapshot schema, string path) =>
            Resolve(row, schema, path) as DateTimeOffset?;

        /// <summary>
        /// 该类里「未软删 / 未隐藏」的行。规则按类名分派，与官方一致：
        /// 难度要自身未 Hidden，且所属谱面集（可能为空）未软删。
        /// </summary>
        public static bool IsLive(IRealmObjectBase row, RealmSchemaSnapshot schema, string className)
        {
            switch (className)
            {
                case OfficialBaselineSchema.BeatmapSet:
                case OfficialBaselineSchema.Score:
                case OfficialBaselineSchema.Skin:
                    return !ResolveBool(row, schema, "DeletePending");

                case OfficialBaselineSchema.Beatmap:
                    return !ResolveBool(row, schema, "Hidden") && !ResolveBool(row, schema, "BeatmapSet.DeletePending");

                default:
                    return true;
            }
        }

        /// <summary>
        /// 枚举某类的存活行；类不存在则空集。
        ///
        /// 先 <c>AsEnumerable()</c> 再过滤：Realm 的 LINQ provider 只认它自己那套表达式，
        /// 直接在 <c>IQueryable</c> 上 <c>Where</c>/<c>Cast</c> 会抛 <c>The method '…' is not supported</c>。
        /// </summary>
        public static IEnumerable<IRealmObjectBase> LiveRows(DynamicRealmSession session, RealmSchemaSnapshot schema, string className)
        {
            if (!schema.HasClass(className))
                return [];

            return DynamicRealmAccess.All(session.Realm, className)
                                     .AsEnumerable()
                                     .Where(row => IsLive(row, schema, className));
        }

        /// <summary>枚举某类的存活行，用于不需要过滤语义的调用方（File / Ruleset / Collection）。</summary>
        public static IEnumerable<IRealmObjectBase> AllRows(DynamicRealmSession session, RealmSchemaSnapshot schema, string className) =>
            schema.HasClass(className)
                ? DynamicRealmAccess.All(session.Realm, className).AsEnumerable()
                : [];
    }
}
