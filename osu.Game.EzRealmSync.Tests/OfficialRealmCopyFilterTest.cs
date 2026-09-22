#if HAS_EZ_OSU_GAME
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm;
using osu.Game.EzRealmSync.Realm.Dynamic;
using osu.Game.EzRealmSync.Tests.TestInfrastructure;
using osu.Game.Scoring;
using Realms;

namespace osu.Game.EzRealmSync.Tests
{
    /// <summary>
    /// 「转回官方版」的行过滤：哪些行搬、哪些行留在备份里。
    ///
    /// 逐行判定而不是只看总数：官方端读不到的行必须一行都不落过去（外部托管的谱面集、Ez 规则集的难度、
    /// Ez 判定语义或 Ez 专用 mod 的成绩），能用的行一行都不能少。
    /// </summary>
    [TestFixture]
    public class OfficialRealmCopyFilterTest
    {
        private static readonly Guid kept_set = Guid.Parse("01000000-0000-0000-0000-000000000001");
        private static readonly Guid external_set = Guid.Parse("01000000-0000-0000-0000-000000000002");
        private static readonly Guid ez_ruleset_set = Guid.Parse("01000000-0000-0000-0000-000000000003");
        private static readonly Guid deleted_set = Guid.Parse("01000000-0000-0000-0000-000000000004");

        private static readonly Guid kept_beatmap = Guid.Parse("02000000-0000-0000-0000-000000000001");
        private static readonly Guid external_beatmap = Guid.Parse("02000000-0000-0000-0000-000000000002");
        private static readonly Guid ez_ruleset_beatmap = Guid.Parse("02000000-0000-0000-0000-000000000003");
        private static readonly Guid deleted_beatmap = Guid.Parse("02000000-0000-0000-0000-000000000004");

        private static readonly Guid kept_score = Guid.Parse("03000000-0000-0000-0000-000000000001");
        private static readonly Guid legacy_mode_score = Guid.Parse("03000000-0000-0000-0000-000000000002");
        private static readonly Guid ez_mod_score = Guid.Parse("03000000-0000-0000-0000-000000000003");
        private static readonly Guid ez_hit_mode_score = Guid.Parse("03000000-0000-0000-0000-000000000004");
        private static readonly Guid ez_health_mode_score = Guid.Parse("03000000-0000-0000-0000-000000000005");
        private static readonly Guid ez_ruleset_score = Guid.Parse("03000000-0000-0000-0000-000000000006");
        private static readonly Guid missing_beatmap_score = Guid.Parse("03000000-0000-0000-0000-000000000007");

        [Test]
        public void Filter_keeps_only_rows_the_official_client_can_use()
        {
            string root = Path.Combine(Path.GetTempPath(), "EzRealmSyncCopyFilter", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "client.realm");

            try
            {
                seedLibrary(path);

                using var session = DynamicRealmSession.OpenDynamic(path, readOnly: true);
                var filter = new OfficialRealmCopyFilter(session);
                filter.BuildIndexes();

                Assert.Multiple(() =>
                {
                    Assert.That(copiesRuleset(session, filter, "osu"), Is.True, "官方规则集必须搬。");
                    Assert.That(copiesRuleset(session, filter, "diva"), Is.False, "Ez 专用规则集不搬。");

                    Assert.That(copies(session, filter, OfficialBaselineSchema.BeatmapSet, kept_set), Is.True, "内部托管的正常谱面集必须搬。");
                    Assert.That(copies(session, filter, OfficialBaselineSchema.BeatmapSet, external_set), Is.False, "外部托管的谱面集官方读不到内容。");
                    Assert.That(copies(session, filter, OfficialBaselineSchema.BeatmapSet, ez_ruleset_set), Is.False, "只含 diva 难度的谱面集在官方端是空集。");
                    Assert.That(copies(session, filter, OfficialBaselineSchema.BeatmapSet, deleted_set), Is.False, "软删的谱面集不搬。");

                    Assert.That(copies(session, filter, OfficialBaselineSchema.Beatmap, kept_beatmap), Is.True);
                    Assert.That(copies(session, filter, OfficialBaselineSchema.Beatmap, external_beatmap), Is.False, "所属集被滤掉的难度跟着滤掉。");
                    Assert.That(copies(session, filter, OfficialBaselineSchema.Beatmap, ez_ruleset_beatmap), Is.False, "diva 难度官方打不开。");
                    Assert.That(copies(session, filter, OfficialBaselineSchema.Beatmap, deleted_beatmap), Is.False);

                    Assert.That(copies(session, filter, OfficialBaselineSchema.Score, kept_score), Is.True, "官方规则集 + Lazer 判定 + 官方 mod 的成绩必须搬。");
                    Assert.That(copies(session, filter, OfficialBaselineSchema.Score, legacy_mode_score), Is.True, "迁移前的老成绩按 Lazer 读，仍算官方语义。");
                    Assert.That(copies(session, filter, OfficialBaselineSchema.Score, ez_mod_score), Is.False, "带 Ez 专用 mod 的成绩官方解析不出来。");
                    Assert.That(copies(session, filter, OfficialBaselineSchema.Score, ez_hit_mode_score), Is.False, "Ez 判定语义的成绩官方重算会算错。");
                    Assert.That(copies(session, filter, OfficialBaselineSchema.Score, ez_health_mode_score), Is.False, "Ez 血条语义的成绩同样不搬。");
                    Assert.That(copies(session, filter, OfficialBaselineSchema.Score, ez_ruleset_score), Is.False, "diva 规则集的成绩官方打不开。");
                    Assert.That(copies(session, filter, OfficialBaselineSchema.Score, missing_beatmap_score), Is.False, "难度没搬过去的成绩搬过去就是悬空行。");
                });

                assertMetadataFollowsItsBeatmaps(session, filter);
            }
            finally
            {
                RealmNativeLifetime.Flush();

                try
                {
                    Directory.Delete(root, recursive: true);
                }
                catch
                {
                    // 清理失败不影响断言结果。
                }
            }
        }

        private static void assertMetadataFollowsItsBeatmaps(DynamicRealmSession session, OfficialRealmCopyFilter filter)
        {
            foreach (IRealmObjectBase beatmap in DynamicRealmAccess.All(session.Realm, OfficialBaselineSchema.Beatmap).AsEnumerable())
            {
                if (DynamicRealmAccess.Get<IRealmObjectBase>(beatmap, "Metadata") is not { } metadata)
                    continue;

                bool beatmapKept = copies(session, filter, OfficialBaselineSchema.Beatmap, DynamicRealmAccess.Get<Guid>(beatmap, "ID"));

                // 被滤掉的集里的元数据若还搬过去，官方库里就多一堆没人引用的行。
                Assert.That(
                    filter.ShouldCopy(OfficialBaselineSchema.BeatmapMetadata, metadata),
                    Is.EqualTo(beatmapKept),
                    $"元数据 {DynamicRealmAccess.GetString(metadata, "Title")} 的取舍与它的难度不一致。");
            }
        }

        /// <summary>
        /// 用同步写入器把「谱面集 / 难度 / 成绩」落进空 Ez 库，再 typed 写 Ez 扩展列。
        /// 不依赖官方样本与 Worker：过滤规则只看行本身，夹具也就能自己造全。
        /// </summary>
        private static void seedLibrary(string path)
        {
            RealmNativeLifetime.CreateEmptyRealmFile(path, (ulong)RealmAccess.EzFileSchemaVersion);

            using (var access = TypedRealmAccess.OpenForMutation(path, RealmAccess.EzFileSchemaVersion))
                access.Run(_ => { });

            RealmNativeLifetime.Flush();

            var bundle = new RealmSyncApplyBundle
            {
                BeatmapSets =
                [
                    setWithBeatmap(kept_set, kept_beatmap, "hash-kept", "osu"),
                    setWithBeatmap(external_set, external_beatmap, "hash-external", "osu"),
                    setWithBeatmap(ez_ruleset_set, ez_ruleset_beatmap, "hash-ez-ruleset", "diva"),
                    setWithBeatmap(deleted_set, deleted_beatmap, "hash-deleted", "osu"),
                ],
                Scores =
                [
                    score(kept_score, "hash-kept", "osu", """[{"acronym":"HD"}]"""),
                    score(legacy_mode_score, "hash-kept", "osu", """[{"acronym":"DT"}]"""),
                    score(ez_mod_score, "hash-kept", "osu", """[{"acronym":"NCl"}]"""),
                    score(ez_hit_mode_score, "hash-kept", "osu", """[{"acronym":"HD"}]"""),
                    score(ez_health_mode_score, "hash-kept", "osu", """[{"acronym":"HD"}]"""),
                    score(ez_ruleset_score, "hash-ez-ruleset", "diva", """[{"acronym":"HD"}]"""),
                    score(missing_beatmap_score, "hash-external", "osu", """[{"acronym":"HD"}]"""),
                ],
            };

            var result = DynamicBaselineWriter.Apply(
                new ApplyRequest { ItemIds = bundle.BeatmapSets.Select(s => s.ID).Concat(bundle.Scores.Select(s => s.ID)).ToList(), CreateBackup = false },
                bundle,
                path);

            Assert.That(result.SkipReasons, Is.Empty, "夹具写入被跳过了，测试前提不成立。");

            writeEzOnlyColumns(path);
        }

        private static void writeEzOnlyColumns(string path)
        {
            using var access = TypedRealmAccess.OpenForMutation(path, RealmAccess.EzFileSchemaVersion);

            access.Write(realm =>
            {
                var external = realm.All<BeatmapSetInfo>().Single(s => s.ID == external_set);
                external.HostingKind = BeatmapSetHostingKind.External;
                external.ExternalContentRoot = @"D:\EzExternal\filter";

                realm.All<BeatmapSetInfo>().Single(s => s.ID == deleted_set).DeletePending = true;

                realm.All<ScoreInfo>().Single(s => s.ID == legacy_mode_score).ManiaHitMode = -1;
                realm.All<ScoreInfo>().Single(s => s.ID == ez_hit_mode_score).ManiaHitMode = (int)EzEnumHitMode.O2Jam;
                realm.All<ScoreInfo>().Single(s => s.ID == ez_health_mode_score).ManiaHealthMode = (int)EzEnumHealthMode.O2JamHard;
            });

            RealmNativeLifetime.Flush();
        }

        private static OfficialBeatmapSetDto setWithBeatmap(Guid setId, Guid beatmapId, string hash, string ruleset) => new()
        {
            ID = setId,
            Hash = hash,
            DateAdded = DateTimeOffset.UtcNow,
            Beatmaps =
            [
                new OfficialBeatmapDto
                {
                    ID = beatmapId,
                    DifficultyName = "Normal",
                    RulesetShortName = ruleset,
                    Hash = hash,
                    MD5Hash = hash + "-md5",
                    Metadata = new OfficialBeatmapMetadataDto { Title = "song " + hash, Artist = "artist" },
                },
            ],
        };

        private static OfficialScoreDto score(Guid id, string beatmapHash, string ruleset, string modsJson) => new()
        {
            ID = id,
            BeatmapHash = beatmapHash,
            RulesetShortName = ruleset,
            ClientVersion = "2026.917.0",
            Hash = "replay-" + id,
            TotalScore = 1_000_000,
            MaxCombo = 500,
            Accuracy = 0.99,
            Date = DateTimeOffset.UtcNow,
            User = new OfficialRealmUserDto { OnlineID = 2, Username = "player", CountryString = "CR" },
            ModsJson = modsJson,
            StatisticsJson = "{\"Great\":100}",
        };

        private static bool copies(DynamicRealmSession session, OfficialRealmCopyFilter filter, string className, Guid id)
        {
            IRealmObjectBase? row = DynamicRealmAccess.Find(session.Realm, className, id);
            Assert.That(row, Is.Not.Null, $"夹具里没有 {className} {id}。");

            return filter.ShouldCopy(className, row!);
        }

        private static bool copiesRuleset(DynamicRealmSession session, OfficialRealmCopyFilter filter, string shortName)
        {
            IRealmObjectBase? row = DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.Ruleset, shortName);
            Assert.That(row, Is.Not.Null, $"夹具里没有规则集 {shortName}。");

            return filter.ShouldCopy(OfficialBaselineSchema.Ruleset, row!);
        }
    }
}
#endif
