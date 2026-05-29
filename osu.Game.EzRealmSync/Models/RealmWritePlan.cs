namespace osu.Game.EzRealmSync.Models
{
    /// <summary>
    /// 从 UI 的 A（源）到 B（目标）的一次写入/扫描计划。A/B 与 ppy/Ez 种类无关。
    /// </summary>
    public sealed class RealmWritePlan
    {
        public required string SourceRealmFilePath { get; init; }

        public required string TargetRealmFilePath { get; init; }

        public RealmDiskSchemaKind SourceKind { get; init; }

        public RealmDiskSchemaKind TargetKind { get; init; }

        /// <summary>仅当写入 ppy 裸 schema 目标且源为 Ez 扩展库时剥离 Ez 列。</summary>
        public bool StripEzFieldsForTarget =>
            SourceKind == RealmDiskSchemaKind.EzExtended && TargetKind == RealmDiskSchemaKind.PpyClient;

        /// <summary>供 Mock / 旧 API 使用的方向枚举。</summary>
        public SyncDirection LegacyDirection { get; init; }

        /// <summary>
        /// <paramref name="endpointA"/> = 数据源 A，<paramref name="endpointB"/> = 目标 B。
        /// </summary>
        public static bool TryFromEndpoints(RealmFileEntry endpointA, RealmFileEntry endpointB, out RealmWritePlan? plan, out string? error)
        {
            plan = null;
            error = null;

            var kindA = RealmSchemaSafety.Classify(endpointA.SchemaVersion);
            var kindB = RealmSchemaSafety.Classify(endpointB.SchemaVersion);

            if (kindA == RealmDiskSchemaKind.Unknown)
            {
                error = "无法识别 A 端的 schema；请刷新列表或重新注册该 Realm 文件。";
                return false;
            }

            if (kindB == RealmDiskSchemaKind.Unknown)
            {
                error = "无法识别 B 端的 schema；请刷新列表或重新注册该 Realm 文件。";
                return false;
            }

            if (!TryMapLegacyDirection(kindA, kindB, out var legacyDirection))
            {
                error = "无法确定 A→B 的库访问方式。";
                return false;
            }

            plan = new RealmWritePlan
            {
                SourceRealmFilePath = Path.GetFullPath(endpointA.FilePath),
                TargetRealmFilePath = Path.GetFullPath(endpointB.FilePath),
                SourceKind = kindA,
                TargetKind = kindB,
                LegacyDirection = legacyDirection,
            };

            return true;
        }

        public static bool TryMapLegacyDirection(RealmDiskSchemaKind sourceKind, RealmDiskSchemaKind targetKind, out SyncDirection direction)
        {
            switch (sourceKind, targetKind)
            {
                case (RealmDiskSchemaKind.EzExtended, RealmDiskSchemaKind.PpyClient):
                    direction = SyncDirection.EzToOfficial;
                    return true;

                case (RealmDiskSchemaKind.PpyClient, RealmDiskSchemaKind.EzExtended):
                    direction = SyncDirection.OfficialToEz;
                    return true;

                case (RealmDiskSchemaKind.EzExtended, RealmDiskSchemaKind.EzExtended):
                    direction = SyncDirection.EzToEz;
                    return true;

                case (RealmDiskSchemaKind.PpyClient, RealmDiskSchemaKind.PpyClient):
                    direction = SyncDirection.PpyToPpy;
                    return true;

                default:
                    direction = default;
                    return false;
            }
        }

        public PathConfiguration ToLegacyPathConfiguration()
        {
            string sourceRoot = RealmWorkspacePaths.ResolveStorageRoot(SourceRealmFilePath);
            string targetRoot = RealmWorkspacePaths.ResolveStorageRoot(TargetRealmFilePath);

            return LegacyDirection switch
            {
                SyncDirection.EzToOfficial => new PathConfiguration
                {
                    EzDataPath = sourceRoot,
                    OfficialDataPath = targetRoot,
                    SourceRealmFilePath = SourceRealmFilePath,
                    TargetRealmFilePath = TargetRealmFilePath,
                },
                SyncDirection.OfficialToEz => new PathConfiguration
                {
                    EzDataPath = targetRoot,
                    OfficialDataPath = sourceRoot,
                    SourceRealmFilePath = SourceRealmFilePath,
                    TargetRealmFilePath = TargetRealmFilePath,
                },
                SyncDirection.EzToEz => new PathConfiguration
                {
                    EzDataPath = sourceRoot,
                    OfficialDataPath = targetRoot,
                    SourceRealmFilePath = SourceRealmFilePath,
                    TargetRealmFilePath = TargetRealmFilePath,
                },
                SyncDirection.PpyToPpy => new PathConfiguration
                {
                    EzDataPath = sourceRoot,
                    OfficialDataPath = targetRoot,
                    SourceRealmFilePath = SourceRealmFilePath,
                    TargetRealmFilePath = TargetRealmFilePath,
                },
                _ => new PathConfiguration
                {
                    SourceRealmFilePath = SourceRealmFilePath,
                    TargetRealmFilePath = TargetRealmFilePath,
                },
            };
        }
    }
}
