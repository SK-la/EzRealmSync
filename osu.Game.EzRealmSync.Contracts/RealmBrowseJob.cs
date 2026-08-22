namespace osu.Game.EzRealmSync.Contracts
{
    /// <summary>ReadSidecar Worker 输入：按 reader lib 只读构建数据 Tab 浏览快照。</summary>
    public sealed class RealmBrowseJob
    {
        public required string ReaderLibDirectory { get; set; }

        /// <summary>profile 共享传递依赖目录（official → readers/_shared/official/lib；ez → host lib）。</summary>
        public string? SharedLibDirectory { get; set; }

        public required string RealmFilePath { get; set; }

        public int PinnedDiskSchemaVersion { get; set; }

        /// <summary><c>official</c> 或 <c>ez</c>。</summary>
        public required string Profile { get; set; }

        public required string RealmId { get; set; }

        public required string DisplayName { get; set; }
    }

    public sealed class RealmBrowseResult
    {
        public bool Success { get; set; }

        public string? ErrorMessage { get; set; }

        public RealmBrowseSnapshotDto? Snapshot { get; set; }
    }

    public sealed class RealmBrowseSnapshotDto
    {
        public string RealmId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public List<RealmBrowseClassGroupDto> Classes { get; set; } = new List<RealmBrowseClassGroupDto>();
    }

    public sealed class RealmBrowseClassGroupDto
    {
        public string Class { get; set; } = string.Empty;

        public List<RealmBrowseColumnDto> Columns { get; set; } = new List<RealmBrowseColumnDto>();

        public List<RealmBrowseRowDto> Rows { get; set; } = new List<RealmBrowseRowDto>();
    }

    public sealed class RealmBrowseColumnDto
    {
        public string Header { get; set; } = string.Empty;

        public string PropertyKey { get; set; } = string.Empty;

        public string? TypeHint { get; set; }
    }

    public sealed class RealmBrowseRowDto
    {
        public Guid Id { get; set; }

        public Dictionary<string, string> Cells { get; set; } = new Dictionary<string, string>();
    }
}
