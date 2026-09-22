using System.Collections;
using osu.Game.EzRealmSync.Models;
using Realms;
using Realms.Schema;

namespace osu.Game.EzRealmSync.Realm.Dynamic
{
    /// <summary>
    /// 跨库逐格搬运：以<b>目标</b> schema 为准，把源库里同名类、同名列的格子搬过去；源多出来的表与列
    /// 一律不搬。用于「把 Ez 数据写进一份官方库」，也用于「按官方 N 的 schema 生成官方库」。
    ///
    /// 与 <see cref="DynamicBaselineWriter"/> 的分工：那个按官方基线白名单搬固定几类业务对象（同步），
    /// 这个按 schema 搬全库（转官方/建库），因而对双方都是新版本时无需改代码。
    ///
    /// 搬运规则：
    /// <list type="bullet">
    /// <item>非嵌入类：目标先建好整类行（主键就位），列在第二遍填——链接可能指回尚未填完的行，先有骨架才不会写空。</item>
    /// <item>嵌入类：不单独建行，只在宿主的那一列上创建（一对一）或往列表里追加（一对多）。</item>
    /// <item>反向链接列：不搬。它由对方的链接列决定，目标库自己会算出来。</item>
    /// <item>源有、目标 schema 没有的列（Ez 列、以及目标版本还没引入的列）：不搬，计入报告。</item>
    /// </list>
    ///
    /// 调用方负责事务边界之外的替换/备份；本方法只写目标库。
    /// </summary>
    public static class DynamicRealmCopier
    {
        public static DynamicCopyResult Copy(
            DynamicRealmSession source,
            DynamicRealmSession target,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(target);

            RealmSchemaSnapshot sourceSchema = DynamicSchemaReader.Read(source);
            RealmSchemaSnapshot targetSchema = DynamicSchemaReader.Read(target);

            var context = new CopyContext(source, target, sourceSchema, targetSchema, progress);

            using (var transaction = target.Realm.BeginWrite())
            {
                context.buildSkeleton(cancellationToken);
                context.fill(cancellationToken);
                transaction.Commit();
            }

            progress?.Report(new ScanProgress { Progress = 1, Message = "搬运完成" });
            return context.ToResult();
        }

        private sealed class CopyContext
        {
            private readonly DynamicRealmSession source;
            private readonly DynamicRealmSession target;
            private readonly RealmSchemaSnapshot sourceSchema;
            private readonly RealmSchemaSnapshot targetSchema;
            private readonly IProgress<ScanProgress>? progress;

            /// <summary>
            /// 源行 → 目标行。按<b>行身份</b>记（Realm 包装对象的 Equals/GetHashCode 就是这个语义，无主键的类也适用）：
            /// 同一行两次读出来是不同包装实例，按引用记会让共享行（多条难度共用一条元数据）在目标库里被复制成多行。
            /// </summary>
            private readonly Dictionary<IRealmObjectBase, IRealmObjectBase> targetBySource = new();

            private readonly List<(RealmClassSchema Class, IRealmObjectBase Source)> rows = new();
            private readonly Dictionary<string, int> rowsPerClass = new(StringComparer.Ordinal);
            private readonly List<string> notes = new();

            public CopyContext(
                DynamicRealmSession source,
                DynamicRealmSession target,
                RealmSchemaSnapshot sourceSchema,
                RealmSchemaSnapshot targetSchema,
                IProgress<ScanProgress>? progress)
            {
                this.source = source;
                this.target = target;
                this.sourceSchema = sourceSchema;
                this.targetSchema = targetSchema;
                this.progress = progress;
            }

            public DynamicCopyResult ToResult() =>
                new(rows.Count, new Dictionary<string, int>(rowsPerClass, StringComparer.Ordinal), notes.ToArray());

            /// <summary>建好目标库的整类行，并登记「源行 → 目标行」。嵌入类不在这里出现。</summary>
            public void buildSkeleton(CancellationToken cancellationToken)
            {
                int done = 0;

                foreach (RealmClassSchema targetClass in targetSchema.Classes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (targetClass.IsEmbedded)
                        continue;

                    if (!sourceSchema.TryFindClass(targetClass.Name, out _))
                    {
                        notes.Add($"源库没有类 {targetClass.Name}，目标该表保持为空。");
                        continue;
                    }

                    RealmClassSchema sourceClass = sourceSchema.Find(targetClass.Name)!;

                    foreach (IRealmObjectBase row in DynamicRealmAccess.All(source.Realm, targetClass.Name).AsEnumerable())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        targetBySource[row] = createRow(targetClass, sourceClass, row);
                        rows.Add((targetClass, row));
                    }

                    rowsPerClass[targetClass.Name] = rows.Count(r => string.Equals(r.Class.Name, targetClass.Name, StringComparison.Ordinal));

                    done++;
                    progress?.Report(new ScanProgress
                    {
                        Progress = 0.5 * done / Math.Max(1, targetSchema.ClassCount),
                        Message = $"正在建立目标库结构（{targetClass.Name}）…",
                    });
                }
            }

            /// <summary>第二遍：逐行逐列填格。此时目标库已有全部行，链接指向的目标必定存在。</summary>
            public void fill(CancellationToken cancellationToken)
            {
                int done = 0;

                foreach ((RealmClassSchema targetClass, IRealmObjectBase sourceRow) in rows)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!sourceSchema.TryFindClass(targetClass.Name, out RealmClassSchema? sourceClass))
                        continue;

                    fillRow(targetClass, sourceClass, sourceRow, targetBySource[sourceRow]);

                    done++;
                    progress?.Report(new ScanProgress
                    {
                        Progress = 0.5 + 0.5 * done / Math.Max(1, rows.Count),
                        Message = $"正在搬运数据（{done}/{rows.Count}）…",
                    });
                }
            }

            private IRealmObjectBase createRow(RealmClassSchema targetClass, RealmClassSchema sourceClass, IRealmObjectBase sourceRow)
            {
                if (targetClass.PrimaryKeyProperty is not { } primaryKey
                    || !sourceClass.TryFindProperty(primaryKey, out RealmPropertySchema? sourceKeyProperty))
                {
                    return DynamicRealmAccess.Create(target.Realm, targetClass.Name);
                }

                object? value = DynamicValueCodec.Read(sourceRow, sourceKeyProperty);

                return value switch
                {
                    Guid guid => DynamicRealmAccess.Create(target.Realm, targetClass.Name, guid),
                    string text => DynamicRealmAccess.Create(target.Realm, targetClass.Name, text),
                    long number => target.Realm.DynamicApi.CreateObject(targetClass.Name, number),
                    null => DynamicRealmAccess.Create(target.Realm, targetClass.Name),
                    _ => throw new InvalidOperationException(
                        $"类 {targetClass.Name} 的主键 {primaryKey} 是 {value.GetType().Name}，暂不支持。"),
                };
            }

            private void fillRow(RealmClassSchema targetClass, RealmClassSchema sourceClass, IRealmObjectBase sourceRow, IRealmObjectBase targetRow)
            {
                foreach (RealmPropertySchema targetProperty in targetClass.Properties)
                {
                    if (targetProperty.IsPrimaryKey || targetProperty.ElementType == PropertyType.LinkingObjects)
                        continue;

                    if (!sourceClass.TryFindProperty(targetProperty.Name, out RealmPropertySchema? sourceProperty))
                    {
                        noteUnmatchedColumn(targetClass.Name, targetProperty.Name);
                        continue;
                    }

                    try
                    {
                        copyColumn(sourceRow, targetRow, sourceProperty, targetProperty);
                    }
                    catch (Exception ex)
                    {
                        // 逐列搬运是反射调用链，异常原样冒出来只剩 "TargetInvocationException"，
                        // 追不到是哪一库哪一列。这里补上上下文再抛，报错信息才可用。
                        throw new InvalidOperationException(
                            $"搬运 {targetClass.Name}.{targetProperty.Name}（源 {sourceProperty.DescribeType()} → 目标 {targetProperty.DescribeType()}）失败：{describe(ex)}",
                            ex);
                    }
                }

                foreach (RealmPropertySchema sourceProperty in sourceClass.Properties)
                {
                    if (!targetClass.HasProperty(sourceProperty.Name) && sourceProperty.ElementType != PropertyType.LinkingObjects)
                        noteExtraSourceColumn(targetClass.Name, sourceProperty.Name);
                }
            }

            /// <summary>
            /// 搬一列。链接列与集合列要先把源对象换成目标对象——<c>RealmValue</c> 里的链接必须指向目标库，
            /// 直接写入源库对象会在提交时炸掉。
            /// </summary>
            private void copyColumn(
                IRealmObjectBase sourceRow,
                IRealmObjectBase targetRow,
                RealmPropertySchema sourceProperty,
                RealmPropertySchema targetProperty)
            {
                if (targetProperty.IsCollection)
                {
                    if (targetProperty.ElementType == PropertyType.Object)
                        copyObjectCollection(sourceRow, targetRow, sourceProperty, targetProperty);
                    else
                        DynamicValueCodec.Write(targetRow, targetProperty, DynamicValueCodec.Read(sourceRow, sourceProperty));

                    return;
                }

                if (targetProperty.ElementType == PropertyType.Object)
                {
                    copySingleObject(sourceRow, targetRow, sourceProperty, targetProperty);
                    return;
                }

                DynamicValueCodec.Write(targetRow, targetProperty, DynamicValueCodec.Read(sourceRow, sourceProperty));
            }

            private void copySingleObject(
                IRealmObjectBase sourceRow,
                IRealmObjectBase targetRow,
                RealmPropertySchema sourceProperty,
                RealmPropertySchema targetProperty)
            {
                if (DynamicValueCodec.Read(sourceRow, sourceProperty) is not IRealmObjectBase linked)
                    return;

                if (!targetSchema.TryFindClass(targetProperty.ObjectType, out RealmClassSchema? targetLinkedClass)
                    || !sourceSchema.TryFindClass(targetProperty.ObjectType, out RealmClassSchema? sourceLinkedClass))
                {
                    notes.Add($"{targetProperty.ObjectType} 不在目标 schema 里，{targetProperty.Name} 链接留空。");
                    return;
                }

                if (targetLinkedClass.IsEmbedded)
                {
                    IRealmObjectBase embedded = target.Realm.DynamicApi.CreateEmbeddedObjectForProperty(targetRow, targetProperty.Name);
                    fillRow(targetLinkedClass, sourceLinkedClass, linked, embedded);
                    return;
                }

                if (targetBySource.TryGetValue(linked, out IRealmObjectBase? mapped))
                {
                    DynamicValueCodec.Write(targetRow, targetProperty, mapped);
                    return;
                }

                // 骨架是按类整体建的，走到这里说明链接指向的行在源库里存在但没被枚举到（例如被隐藏的类）。
                // 不猜、不新建：写一条空链接并记录，避免在目标库里造出"没人引用的行"。
                notes.Add($"{targetProperty.ObjectType} 的链接目标未在目标库建立，{targetProperty.Name} 链接留空。");
            }

            private void copyObjectCollection(
                IRealmObjectBase sourceRow,
                IRealmObjectBase targetRow,
                RealmPropertySchema sourceProperty,
                RealmPropertySchema targetProperty)
            {
                object? sourceList = DynamicRealmAccess.GetListRaw(sourceRow, sourceProperty.Name);
                if (sourceList is not IEnumerable items)
                    return;

                if (!targetSchema.TryFindClass(targetProperty.ObjectType, out RealmClassSchema? targetElementClass)
                    || !sourceSchema.TryFindClass(targetProperty.ObjectType, out RealmClassSchema? sourceElementClass))
                {
                    notes.Add($"{targetProperty.ObjectType} 不在目标 schema 里，{targetProperty.Name} 列表整体留空。");
                    return;
                }

                object? targetList = DynamicRealmAccess.GetListRaw(targetRow, targetProperty.Name);

                if (targetList == null)
                    return;

                DynamicRealmAccess.ClearList(targetList);

                foreach (object? item in items)
                {
                    if (item is not IRealmObjectBase linked)
                        continue;

                    if (targetElementClass.IsEmbedded)
                    {
                        IRealmObjectBase embedded = target.Realm.DynamicApi.AddEmbeddedObjectToList(targetList);
                        fillRow(targetElementClass, sourceElementClass, linked, embedded);
                        continue;
                    }

                    if (targetBySource.TryGetValue(linked, out IRealmObjectBase? mapped))
                        DynamicRealmAccess.AddToList(targetList, mapped);
                    else
                        notes.Add($"{targetProperty.ObjectType} 的列表元素未在目标库建立，{targetProperty.Name} 已跳过该项。");
                }
            }

            private void noteUnmatchedColumn(string className, string propertyName)
            {
                if (sourceSchema.HasClass(className))
                    notes.Add($"{className}.{propertyName}：目标有、源没有，留默认值。");
            }

            /// <summary>反射调用链会把真实异常包在 <see cref="System.Reflection.TargetInvocationException"/> 里。</summary>
            private static string describe(Exception ex) =>
                ex is System.Reflection.TargetInvocationException { InnerException: { } inner }
                    ? $"{inner.GetType().Name} {inner.Message}"
                    : $"{ex.GetType().Name} {ex.Message}";

            private void noteExtraSourceColumn(string className, string propertyName)
            {
                string note = $"{className}.{propertyName}：源有、目标 schema 没有，未搬运。";

                if (!notes.Contains(note, StringComparer.Ordinal))
                    notes.Add(note);
            }
        }
    }

    /// <summary>搬运结果：搬了多少行、每类多少行，以及被跳过的列/链接（Ez 列、目标版本没有的列都记这里）。</summary>
    public sealed record DynamicCopyResult(
        int Rows,
        IReadOnlyDictionary<string, int> RowsPerClass,
        IReadOnlyList<string> Notes);
}
