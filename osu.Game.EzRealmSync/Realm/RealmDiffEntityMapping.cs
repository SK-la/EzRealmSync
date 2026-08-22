using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    public static class RealmDiffEntityMapping
    {
        public static RealmDiffEntityDto ToDto(RealmDiffEntity entity) => new RealmDiffEntityDto
        {
            Id = entity.Id,
            EntityKind = entity.EntityKind.ToString(),
            Hash = entity.Hash,
            Title = entity.Title,
            Artist = entity.Artist,
            Ruleset = entity.Ruleset,
            Date = entity.Date,
            OnlineId = entity.OnlineId,
            DifficultyName = entity.DifficultyName,
            CollectionBeatmapCount = entity.CollectionBeatmapCount,
            CollectionHashFingerprint = entity.CollectionHashFingerprint,
        };

        public static RealmDiffEntity FromDto(RealmDiffEntityDto dto) => new RealmDiffEntity
        {
            Id = dto.Id,
            EntityKind = Enum.TryParse<EntityKind>(dto.EntityKind, out var kind) ? kind : EntityKind.BeatmapSet,
            Hash = dto.Hash,
            Title = dto.Title,
            Artist = dto.Artist,
            Ruleset = dto.Ruleset,
            Date = dto.Date,
            OnlineId = dto.OnlineId,
            DifficultyName = dto.DifficultyName,
            CollectionBeatmapCount = dto.CollectionBeatmapCount,
            CollectionHashFingerprint = dto.CollectionHashFingerprint,
        };

        public static RealmDiffSnapshot FromResult(RealmReadResult result) => new RealmDiffSnapshot
        {
            Entities = result.Entities.Select(FromDto).ToArray(),
        };
    }
}
