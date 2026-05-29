using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 用于双库 Diff 的规范化行（与 Realm 模型解耦，便于单元测试）。
    /// </summary>
    public sealed class RealmDiffEntity
    {
        public Guid Id { get; init; }

        public EntityKind EntityKind { get; init; }

        public string Hash { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string Artist { get; init; } = string.Empty;

        public string Ruleset { get; init; } = string.Empty;

        public DateTimeOffset? Date { get; init; }

        public long? OnlineId { get; init; }

        public string? DifficultyName { get; init; }
    }
}
