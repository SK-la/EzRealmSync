#if HAS_EZ_OSU_GAME
using osu.Game.Database;
using osu.Game.Models;
using osu.Game.Skinning;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 版本迁移 / 转官方时，<see cref="RealmFile"/> 与 <see cref="SkinInfo"/> 表不应被主流程改写：
    /// 处理前在内存快照，处理完原样写回（转官方时剔除 Ez 独有受保护皮肤）。
    /// </summary>
    public static class RealmAuxiliaryTablePreserver
    {
        public sealed class Snapshot
        {
            public required IReadOnlyList<string> FileHashes { get; init; }

            public required IReadOnlyList<SkinInfo> Skins { get; init; }

            public int FileCount => FileHashes.Count;

            public int SkinCount => Skins.Count;

            public override string ToString() => $"files={FileCount}, skins={SkinCount}";
        }

        public static Snapshot Capture(RealmAccess access)
        {
            Snapshot? snapshot = null;

            access.Run(realm =>
            {
                var hashes = new List<string>();
                foreach (var file in realm.All<RealmFile>())
                    hashes.Add(file.Hash);

                var skins = new List<SkinInfo>();
                foreach (var skin in realm.All<SkinInfo>())
                {
                    if (!skin.DeletePending)
                        skins.Add(skin.Detach());
                }

                snapshot = new Snapshot
                {
                    FileHashes = hashes,
                    Skins = skins,
                };
            });

            return snapshot ?? new Snapshot { FileHashes = Array.Empty<string>(), Skins = Array.Empty<SkinInfo>() };
        }

        public static void Restore(RealmAccess access, Snapshot snapshot, bool filterEzOnlyProtectedSkins, CancellationToken cancellationToken = default)
        {
            var skinsToRestore = new List<SkinInfo>();
            if (filterEzOnlyProtectedSkins)
            {
                foreach (var skin in snapshot.Skins)
                {
                    if (!IsEzOnlyProtectedSkin(skin))
                        skinsToRestore.Add(skin);
                }
            }
            else
            {
                skinsToRestore.AddRange(snapshot.Skins);
            }

            const int file_batch = 8_000;

            for (int offset = 0; offset < snapshot.FileHashes.Count; offset += file_batch)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = snapshot.FileHashes.Skip(offset).Take(file_batch).ToList();

                access.Write(realm =>
                {
                    foreach (string hash in batch)
                    {
                        if (realm.Find<RealmFile>(hash) == null)
                            realm.Add(new RealmFile { Hash = hash }, true);
                    }
                });
            }

            foreach (var skin in skinsToRestore)
            {
                cancellationToken.ThrowIfCancellationRequested();

                access.Write(realm =>
                {
                    if (realm.Find<SkinInfo>(skin.ID) != null)
                        return;

                    linkFiles(realm, skin.Files);
                    realm.Add(skin);
                });
            }
        }

        public static bool IsEzOnlyProtectedSkin(SkinInfo skin) =>
            skin.ID == ez2_skin_id
            || skin.ID == ez_style_pro_skin_id
            || skin.ID == sbi_skin_id;

        private static readonly Guid ez2_skin_id = new("fc372386-381d-4f8e-897a-c1d89ef39f9c");
        private static readonly Guid ez_style_pro_skin_id = new("1E70839C-C0D8-4DBF-B747-0C08C89D412B");
        private static readonly Guid sbi_skin_id = new("fc372386-381d-4f8e-897a-c1d89ef39f2c");

        private static void linkFiles(RealmInstance realm, IList<RealmNamedFileUsage> files)
        {
            for (int i = 0; i < files.Count; i++)
            {
                var usage = files[i];
                string hash = usage.File.Hash;
                var managedFile = realm.Find<RealmFile>(hash) ?? realm.Add(new RealmFile { Hash = hash }, true);
                files[i] = new RealmNamedFileUsage(managedFile, usage.Filename);
            }
        }
    }
}
#endif
