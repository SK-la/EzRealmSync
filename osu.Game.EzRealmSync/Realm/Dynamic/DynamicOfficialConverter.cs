using osu.Game.EzRealmSync.Errors;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm.Dynamic
{
    /// <summary>收窄结果：搬了多少行、丢掉多少 Ez 表 / Ez 列（丢掉的只存在于备份里）。</summary>
    public sealed record OfficialConvertStats(
        int UpstreamVersion,
        int RowsWritten,
        IReadOnlyList<string> DroppedClasses,
        IReadOnlyList<string> DroppedColumns,
        IReadOnlyList<string> Notes);

    /// <summary>
    /// 「转回官方版」的实际动作：把一份 Ez 库**收窄**成官方 N 的新库（写到调用方给的临时/目标路径）。
    ///
    /// 为什么是「新建 + 逐格搬运」而不是「就地改 schema」：Realm 拒绝把文件头的 schema 版本调低
    /// （<c>Provided schema version 51 is less than last set version 52010</c>），所以唯一能产出官方号
    /// 文件的做法是按官方 schema 落一份新库，再把公共表公共列的数据搬过去。Ez 表、Ez 列不搬，
    /// 于是产物恰好是官方 schema——官方客户端打开时不需要迁移，也看不到任何 Ez 痕迹。
    ///
    /// 搬运后立刻回读产物自检（schema 必须与官方逐列一致、公共类行数必须与源一致）：这一步失败就
    /// 不覆盖原文件，避免用户拿到一份「看起来转成功、其实掉了数据」的库。
    /// </summary>
    public static class DynamicOfficialConverter
    {
        public static OfficialConvertStats Convert(
            string sourceRealmPath,
            OfficialSchemaSource official,
            string targetRealmPath,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceRealmPath);
            ArgumentNullException.ThrowIfNull(official);
            ArgumentException.ThrowIfNullOrWhiteSpace(targetRealmPath);

            var sourceCounts = new Dictionary<string, long>(StringComparer.Ordinal);
            var droppedClasses = new List<string>();
            var droppedColumns = new List<string>();
            DynamicCopyResult copy;

            progress?.Report(new ScanProgress { Progress = 0.1, Message = $"正在按官方 {official.UpstreamVersion} schema 建库…" });

            // 目标必须是**新库**：已存在的文件上按官方 schema 打开等于就地改 schema，而 Realm 拒绝把
            // 版本降回官方号（"Provided schema version 51 is less than last set version 52010"）。
            if (File.Exists(targetRealmPath))
                File.Delete(targetRealmPath);

            using (DynamicRealmSession source = RealmAccessGateway.OpenDynamicForRead(sourceRealmPath, out RealmSchemaSnapshot sourceSchema))
            {
                collectDropped(sourceSchema, official.Snapshot, droppedClasses, droppedColumns);

                foreach (RealmClassSchema targetClass in official.Snapshot.Classes)
                {
                    if (!targetClass.IsEmbedded && sourceSchema.HasClass(targetClass.Name))
                        sourceCounts[targetClass.Name] = DynamicRealmAccess.All(source.Realm, targetClass.Name).AsEnumerable().LongCount();
                }

                cancellationToken.ThrowIfCancellationRequested();

                using var target = DynamicRealmSession.OpenDynamicWithSchema(
                    targetRealmPath,
                    RealmSchemaDefinitionFactory.Create(official.Snapshot),
                    (ulong)official.UpstreamVersion,
                    readOnly: false);

                copy = DynamicRealmCopier.Copy(source, target, progress, cancellationToken);
            }

            progress?.Report(new ScanProgress { Progress = 0.95, Message = "正在校验产物…" });
            verify(targetRealmPath, official, sourceCounts);

            return new OfficialConvertStats(official.UpstreamVersion, copy.Rows, droppedClasses, droppedColumns, copy.Notes);
        }

        private static void collectDropped(
            RealmSchemaSnapshot sourceSchema,
            RealmSchemaSnapshot officialSnapshot,
            List<string> droppedClasses,
            List<string> droppedColumns)
        {
            foreach (RealmClassSchema sourceClass in sourceSchema.Classes)
            {
                if (!officialSnapshot.TryFindClass(sourceClass.Name, out RealmClassSchema? officialClass))
                {
                    droppedClasses.Add(sourceClass.Name);
                    continue;
                }

                foreach (RealmPropertySchema property in sourceClass.Properties)
                {
                    if (property.ElementType != Realms.Schema.PropertyType.LinkingObjects && !officialClass.HasProperty(property.Name))
                        droppedColumns.Add($"{sourceClass.Name}.{property.Name}");
                }
            }
        }

        /// <summary>
        /// 产物自检。搬运是逐列反射调用，静默漏一列也能"跑完"，所以这里必须回读磁盘上的产物：
        /// schema 与官方逐列比对、公共类行数与源库逐个比对，任一不符就抛错，让调用方保留原文件。
        /// </summary>
        private static void verify(string producedRealmPath, OfficialSchemaSource official, IReadOnlyDictionary<string, long> sourceCounts)
        {
            using DynamicRealmSession produced = RealmAccessGateway.OpenDynamicForRead(producedRealmPath, out RealmSchemaSnapshot producedSchema);

            if (produced.DiskSchemaVersion != official.UpstreamVersion)
            {
                throw new RealmUserOperationException(
                    RealmUserErrorKind.SchemaModelMismatch,
                    $"产物版本是 {produced.DiskSchemaVersion}，不是官方 {official.UpstreamVersion}。");
            }

            IReadOnlyList<string> differences = producedSchema.FindDifferences(official.Snapshot);

            if (differences.Count > 0)
            {
                throw new RealmUserOperationException(
                    RealmUserErrorKind.SchemaModelMismatch,
                    $"产物 schema 与官方 {official.UpstreamVersion} 不一致：{string.Join("；", differences.Take(10))}");
            }

            foreach ((string className, long expected) in sourceCounts)
            {
                long actual = DynamicRealmAccess.All(produced.Realm, className).AsEnumerable().LongCount();

                if (actual != expected)
                    throw new RealmUserOperationException(RealmUserErrorKind.SchemaModelMismatch, $"产物 {className} 行数 {actual}，源库是 {expected}。");
            }
        }
    }
}
