using System.Text.Json;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Configuration;
using osu.Game.Input.Bindings;
using osu.Game.Models;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Skinning;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.DllVerifier
{
    /// <summary>
    /// 用**真实官方模型**打开一份库，读一遍核心表后退出。
    ///
    /// 与 <c>OfficialWrite</c> Worker 的区别：那个用的是 Ez 侧手抄的官方 schema 镜像，
    /// 只能证明"我们抄的那份 schema 自洽"；这里用的是官方包自带的模型，判定等同于官方客户端。
    ///
    /// 两种打开语义：
    /// <list type="bullet">
    /// <item>不给 <c>pinnedSchemaVersion</c>（默认）：按官方包自己的版本号 + 空迁移回调打开，即
    /// 「官方客户端拿到这份文件会怎样」。文件若含官方 schema 里没有的表/列，realm-core 在
    /// schema 比对阶段就会抛错——这正是要验的东西。</item>
    /// <item>给了 <c>pinnedSchemaVersion</c>：钉死该版本、禁用迁移。只有在"文件版本正好等于官方包版本"
    /// 时才可能成功，用来验证产物与官方 schema 逐列一致（不会靠迁移补齐）。</item>
    /// </list>
    /// </summary>
    internal static class OfficialOpenProbe
    {
        public static VerificationOutcome Run(string jobJson)
        {
            using var document = JsonDocument.Parse(jobJson);
            JsonElement job = document.RootElement;

            string realmPath = JobJson.RequireString(job, "realmPath");
            int pinnedVersion = JobJson.ReadInt(job, "pinnedSchemaVersion", 0);
            bool cleanupPendingDeletions = JobJson.ReadBool(job, "cleanupPendingDeletions", false);

            if (!File.Exists(realmPath))
                throw new FileNotFoundException($"找不到 Realm 文件：{realmPath}", realmPath);

            int? declared = OfficialModelCatalog.DeclaredSchemaVersion;
            bool pinned = pinnedVersion > 0;
            bool readOnly = JobJson.ReadBool(job, "readOnly", pinned);

            int configured = pinned
                ? pinnedVersion
                : declared ?? throw new InvalidOperationException("读不到官方包声明的 schema 版本，请改用 pinnedSchemaVersion 显式指定。");

            if (readOnly && !pinned)
                throw new InvalidOperationException("readOnly 只与 pinnedSchemaVersion 搭配：官方客户端语义会写文件头，不能只读。");

            var config = OfficialModelCatalog.CreateConfiguration(realmPath, (ulong)configured, readOnly, allowMigration: !pinned);

            using RealmInstance realm = OfficialModelCatalog.Open(config);

            var counts = readCoreTables(realm);
            Dictionary<string, int>? cleaned = cleanupPendingDeletions ? cleanupSoftDeleted(realm) : null;

            return new VerificationOutcome
            {
                Success = true,
                DeclaredSchemaVersion = declared,
                ConfiguredSchemaVersion = configured,
                OpenedSchemaVersion = OfficialModelCatalog.TryReadSchemaVersion(realm),
                Counts = counts,
                CleanedPending = cleaned,
            };
        }

        /// <summary>
        /// 读一遍工具会碰的核心表，并顺着链接取一遍值——只数行数的话，链接列坏了也发现不了。
        /// 键用 Realm 里的类名，方便调用方对着 <c>OfficialBaselineSchema</c> 断言。
        /// </summary>
        private static Dictionary<string, int> readCoreTables(RealmInstance realm)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Ruleset"] = realm.All<RulesetInfo>().Count(),
                ["File"] = realm.All<RealmFile>().Count(),
            };

            int sets = 0;

            foreach (BeatmapSetInfo set in realm.All<BeatmapSetInfo>())
            {
                sets++;

                _ = set.Hash;
                _ = set.Protected;
                _ = set.StatusInt;
                _ = set.DateAdded;

                foreach (BeatmapInfo beatmap in set.Beatmaps)
                {
                    _ = beatmap.DifficultyName;
                    _ = beatmap.Hash;
                    _ = beatmap.Hidden;
                    _ = beatmap.StarRating;
                    _ = beatmap.OnlineID;
                    _ = beatmap.Ruleset.ShortName;
                    _ = beatmap.Metadata.Title;
                    _ = beatmap.Metadata.Artist;
                    _ = beatmap.Metadata.Author.Username;
                    _ = beatmap.Difficulty.OverallDifficulty;
                }

                foreach (RealmNamedFileUsage file in set.Files)
                {
                    _ = file.Filename;
                    _ = file.File.Hash;
                }
            }

            counts["BeatmapSet"] = sets;
            counts["Beatmap"] = realm.All<BeatmapInfo>().Count();

            int scores = 0;

            foreach (ScoreInfo score in realm.All<ScoreInfo>())
            {
                scores++;

                _ = score.Hash;
                _ = score.BeatmapHash;
                _ = score.TotalScore;
                _ = score.MaxCombo;
                _ = score.Accuracy;
                _ = score.ModsJson;
                _ = score.StatisticsJson;
                _ = score.Date;
                _ = score.Ruleset.ShortName;
                _ = score.RealmUser.Username;
                _ = score.BeatmapInfo?.DifficultyName;

                foreach (RealmNamedFileUsage file in score.Files)
                    _ = file.Filename;
            }

            counts["Score"] = scores;

            int collections = 0;

            foreach (BeatmapCollection collection in realm.All<BeatmapCollection>())
            {
                collections++;

                _ = collection.Name;
                _ = collection.LastModified;
                _ = collection.BeatmapMD5Hashes.Count;
            }

            counts["BeatmapCollection"] = collections;

            int skins = 0;

            foreach (SkinInfo skin in realm.All<SkinInfo>())
            {
                skins++;

                _ = skin.Name;
                _ = skin.Creator;
                _ = skin.InstantiationInfo;
                _ = skin.Hash;

                foreach (RealmNamedFileUsage file in skin.Files)
                    _ = file.Filename;
            }

            counts["Skin"] = skins;

            counts["ModPreset"] = realm.All<ModPreset>().Count();
            counts["KeyBinding"] = realm.All<RealmKeyBinding>().Count();
            counts["RulesetSetting"] = realm.All<RealmRulesetSetting>().Count();

            return counts;
        }

        /// <summary>复刻官方 <c>RealmAccess.cleanupPendingDeletions</c>：软删的行由"下一次打开"真正清掉。</summary>
        private static Dictionary<string, int> cleanupSoftDeleted(RealmInstance realm)
        {
            var cleaned = new Dictionary<string, int>(StringComparer.Ordinal);

            using (var transaction = realm.BeginWrite())
            {
                int scores = 0;

                foreach (ScoreInfo score in realm.All<ScoreInfo>().Where(s => s.DeletePending).ToList())
                {
                    realm.Remove(score);
                    scores++;
                }

                cleaned["Score"] = scores;

                int sets = 0;

                foreach (BeatmapSetInfo set in realm.All<BeatmapSetInfo>().Where(s => s.DeletePending).ToList())
                {
                    foreach (BeatmapInfo beatmap in set.Beatmaps.ToList())
                    {
                        realm.Remove(beatmap.Metadata);
                        realm.Remove(beatmap);
                    }

                    realm.Remove(set);
                    sets++;
                }

                cleaned["BeatmapSet"] = sets;

                int skins = 0;

                foreach (SkinInfo skin in realm.All<SkinInfo>().Where(s => s.DeletePending).ToList())
                {
                    realm.Remove(skin);
                    skins++;
                }

                cleaned["Skin"] = skins;

                int presets = 0;

                foreach (ModPreset preset in realm.All<ModPreset>().Where(s => s.DeletePending).ToList())
                {
                    realm.Remove(preset);
                    presets++;
                }

                cleaned["ModPreset"] = presets;

                transaction.Commit();
            }

            return cleaned;
        }
    }
}
