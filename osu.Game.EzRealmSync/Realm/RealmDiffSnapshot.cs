using osu.Game.EzRealmSync.Models;

namespace osu.Game.EzRealmSync.Realm
{
    public sealed class RealmDiffSnapshot
    {
        public IReadOnlyList<RealmDiffEntity> Entities { get; init; } = Array.Empty<RealmDiffEntity>();

        public IEnumerable<RealmDiffEntity> Enumerate(EntityKind kind) => Entities.Where(e => e.EntityKind == kind);

        public IEnumerable<RealmDiffEntity> EnumerateKinds(IReadOnlyList<EntityKind> kinds)
        {
            if (kinds.Count == 0)
                return Entities;

            var set = kinds.ToHashSet();
            return Entities.Where(e => set.Contains(e.EntityKind));
        }
    }
}
