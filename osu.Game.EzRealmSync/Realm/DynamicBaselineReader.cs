using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Models;
using osu.Game.EzRealmSync.Realm.Dynamic;
using Realms;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>从 DynamicRealm 读取官方基线 Diff / Apply 包。不打开 osu.Game 模型。</summary>
    public static class DynamicBaselineReader
    {
        public static RealmDiffSnapshot ReadDiffSnapshot(
            string realmFilePath,
            IReadOnlyList<EntityKind>? entityKinds = null,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            using var session = DynamicRealmSession.OpenDynamic(realmFilePath, readOnly: true);
            var entities = new List<RealmDiffEntity>();
            var kinds = entityKinds is { Count: > 0 } ? entityKinds.ToHashSet() : null;

            // 顺手把这份库的 schema 落盘：对比/同步本来就要完整打开它，于是"碰过哪个版本就有哪个版本的快照"。
            RealmSchemaSnapshotStore.Default.TryCapture(session);

            progress?.Report(new ScanProgress { Progress = 0.05, Message = "正在读取官方基线…" });

            if (kinds == null || kinds.Contains(EntityKind.BeatmapSet))
            {
                cancellationToken.ThrowIfCancellationRequested();
                entities.AddRange(readBeatmapSets(session));
            }

            progress?.Report(new ScanProgress { Progress = 0.35, Message = "正在读取难度…" });

            if (kinds == null || kinds.Contains(EntityKind.Beatmap))
            {
                cancellationToken.ThrowIfCancellationRequested();
                entities.AddRange(readBeatmaps(session));
            }

            progress?.Report(new ScanProgress { Progress = 0.55, Message = "正在读取收藏夹…" });

            if (kinds == null || kinds.Contains(EntityKind.BeatmapCollection))
            {
                cancellationToken.ThrowIfCancellationRequested();
                entities.AddRange(readCollections(session));
            }

            progress?.Report(new ScanProgress { Progress = 0.75, Message = "正在读取皮肤…" });

            if (kinds == null || kinds.Contains(EntityKind.Skin))
            {
                cancellationToken.ThrowIfCancellationRequested();
                entities.AddRange(readSkins(session));
            }

            if (kinds != null && kinds.Contains(EntityKind.Score))
            {
                cancellationToken.ThrowIfCancellationRequested();
                entities.AddRange(readScores(session));
            }

            progress?.Report(new ScanProgress { Progress = 1, Message = "读取完成" });
            return new RealmDiffSnapshot { Entities = entities };
        }

        public static RealmSyncApplyBundle ExportByIds(string realmFilePath, IReadOnlyList<Guid> itemIds)
        {
            using var session = DynamicRealmSession.OpenDynamic(realmFilePath, readOnly: true);
            var idSet = itemIds.ToHashSet();
            var bundle = new RealmSyncApplyBundle();

            RealmSchemaSnapshotStore.Default.TryCapture(session);

            foreach (Guid id in idSet)
            {
                if (session.HasClass(OfficialBaselineSchema.BeatmapSet)
                    && DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.BeatmapSet, id) is { } set
                    && DynamicRealmAccess.Get<bool>(set, "DeletePending") != true)
                {
                    bundle.BeatmapSets.Add(mapBeatmapSet(set));
                    continue;
                }

                if (session.HasClass(OfficialBaselineSchema.Beatmap)
                    && DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.Beatmap, id) is { } beatmap)
                {
                    var parent = DynamicRealmAccess.Get<IRealmObjectBase>(beatmap, "BeatmapSet");
                    if (parent != null && DynamicRealmAccess.Get<bool>(parent, "DeletePending"))
                        continue;

                    bundle.Beatmaps.Add(mapBeatmap(beatmap));
                    continue;
                }

                if (session.HasClass(OfficialBaselineSchema.BeatmapCollection)
                    && DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.BeatmapCollection, id) is { } collection)
                {
                    bundle.Collections.Add(mapCollection(collection));
                    continue;
                }

                if (session.HasClass(OfficialBaselineSchema.Skin)
                    && DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.Skin, id) is { } skin
                    && DynamicRealmAccess.Get<bool>(skin, "DeletePending") != true)
                {
                    var skinDto = mapSkin(skin);
                    if (skinDto != null)
                        bundle.Skins.Add(skinDto);
                    continue;
                }

                if (session.HasClass(OfficialBaselineSchema.Score)
                    && DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.Score, id) is { } score
                    && DynamicRealmAccess.Get<bool>(score, "DeletePending") != true)
                {
                    bundle.Scores.Add(mapScore(score));
                }
            }

            return bundle;
        }

        private static IEnumerable<RealmDiffEntity> readBeatmapSets(DynamicRealmSession session)
        {
            if (!session.HasClass(OfficialBaselineSchema.BeatmapSet))
                yield break;

            foreach (var set in DynamicRealmAccess.All(session.Realm, OfficialBaselineSchema.BeatmapSet))
            {
                if (DynamicRealmAccess.Get<bool>(set, "DeletePending"))
                    continue;

                IRealmObjectBase? firstBeatmap = DynamicRealmAccess.EnumerateObjects(set, "Beatmaps").FirstOrDefault();
                IRealmObjectBase? metadata = firstBeatmap == null ? null : DynamicRealmAccess.Get<IRealmObjectBase>(firstBeatmap, "Metadata");

                yield return new RealmDiffEntity
                {
                    Id = DynamicRealmAccess.Get<Guid>(set, "ID"),
                    EntityKind = EntityKind.BeatmapSet,
                    Hash = DynamicRealmAccess.GetString(set, "Hash"),
                    Title = DynamicRealmAccess.GetString(metadata, "Title"),
                    Artist = DynamicRealmAccess.GetString(metadata, "Artist"),
                    OnlineId = DynamicRealmAccess.Get<int>(set, "OnlineID"),
                };
            }
        }

        private static IEnumerable<RealmDiffEntity> readBeatmaps(DynamicRealmSession session)
        {
            if (!session.HasClass(OfficialBaselineSchema.Beatmap))
                yield break;

            foreach (var beatmap in DynamicRealmAccess.All(session.Realm, OfficialBaselineSchema.Beatmap))
            {
                if (DynamicRealmAccess.Get<bool>(beatmap, "Hidden"))
                    continue;

                var parent = DynamicRealmAccess.Get<IRealmObjectBase>(beatmap, "BeatmapSet");
                if (parent != null && DynamicRealmAccess.Get<bool>(parent, "DeletePending"))
                    continue;

                var metadata = DynamicRealmAccess.Get<IRealmObjectBase>(beatmap, "Metadata");
                var ruleset = DynamicRealmAccess.Get<IRealmObjectBase>(beatmap, "Ruleset");

                yield return new RealmDiffEntity
                {
                    Id = DynamicRealmAccess.Get<Guid>(beatmap, "ID"),
                    EntityKind = EntityKind.Beatmap,
                    Hash = DynamicRealmAccess.GetString(beatmap, "Hash"),
                    Title = DynamicRealmAccess.GetString(metadata, "Title"),
                    Artist = DynamicRealmAccess.GetString(metadata, "Artist"),
                    Ruleset = DynamicRealmAccess.GetString(ruleset, "ShortName"),
                    DifficultyName = DynamicRealmAccess.GetString(beatmap, "DifficultyName"),
                };
            }
        }

        private static IEnumerable<RealmDiffEntity> readCollections(DynamicRealmSession session)
        {
            if (!session.HasClass(OfficialBaselineSchema.BeatmapCollection))
                yield break;

            foreach (var collection in DynamicRealmAccess.All(session.Realm, OfficialBaselineSchema.BeatmapCollection))
            {
                string[] hashes = DynamicRealmAccess.EnumerateValues<string>(collection, "BeatmapMD5Hashes").ToArray();

                yield return new RealmDiffEntity
                {
                    Id = DynamicRealmAccess.Get<Guid>(collection, "ID"),
                    EntityKind = EntityKind.BeatmapCollection,
                    Title = DynamicRealmAccess.GetString(collection, "Name"),
                    CollectionBeatmapCount = hashes.Length,
                    CollectionHashFingerprint = fingerprint(hashes),
                };
            }
        }

        private static IEnumerable<RealmDiffEntity> readSkins(DynamicRealmSession session)
        {
            if (!session.HasClass(OfficialBaselineSchema.Skin))
                yield break;

            foreach (var skin in DynamicRealmAccess.All(session.Realm, OfficialBaselineSchema.Skin))
            {
                if (DynamicRealmAccess.Get<bool>(skin, "DeletePending"))
                    continue;

                Guid id = DynamicRealmAccess.Get<Guid>(skin, "ID");
                string name = DynamicRealmAccess.GetString(skin, "Name");
                string instantiation = DynamicRealmAccess.GetString(skin, "InstantiationInfo");
                if (OfficialBaselineSchema.IsEzOnlySkin(id, instantiation, name))
                    continue;

                yield return new RealmDiffEntity
                {
                    Id = id,
                    EntityKind = EntityKind.Skin,
                    Hash = DynamicRealmAccess.GetString(skin, "Hash"),
                    Title = name,
                    Artist = DynamicRealmAccess.GetString(skin, "Creator"),
                };
            }
        }

        private static IEnumerable<RealmDiffEntity> readScores(DynamicRealmSession session)
        {
            if (!session.HasClass(OfficialBaselineSchema.Score))
                yield break;

            foreach (var score in DynamicRealmAccess.All(session.Realm, OfficialBaselineSchema.Score))
            {
                if (DynamicRealmAccess.Get<bool>(score, "DeletePending"))
                    continue;

                var beatmap = DynamicRealmAccess.Get<IRealmObjectBase>(score, "BeatmapInfo");
                var metadata = beatmap == null ? null : DynamicRealmAccess.Get<IRealmObjectBase>(beatmap, "Metadata");
                var ruleset = DynamicRealmAccess.Get<IRealmObjectBase>(score, "Ruleset");

                yield return new RealmDiffEntity
                {
                    Id = DynamicRealmAccess.Get<Guid>(score, "ID"),
                    EntityKind = EntityKind.Score,
                    Hash = DynamicRealmAccess.GetString(score, "Hash"),
                    Title = string.IsNullOrEmpty(DynamicRealmAccess.GetString(metadata, "Title"))
                        ? DynamicRealmAccess.GetString(score, "BeatmapHash")
                        : DynamicRealmAccess.GetString(metadata, "Title"),
                    Artist = DynamicRealmAccess.GetString(metadata, "Artist"),
                    Ruleset = DynamicRealmAccess.GetString(ruleset, "ShortName"),
                    Date = DynamicRealmAccess.Get<DateTimeOffset>(score, "Date"),
                };
            }
        }

        private static OfficialScoreDto mapScore(IRealmObjectBase score)
        {
            var ruleset = DynamicRealmAccess.Get<IRealmObjectBase>(score, "Ruleset");

            var dto = new OfficialScoreDto
            {
                ID = DynamicRealmAccess.Get<Guid>(score, "ID"),
                BeatmapHash = DynamicRealmAccess.GetString(score, "BeatmapHash"),
                RulesetShortName = DynamicRealmAccess.GetString(ruleset, "ShortName"),
                ClientVersion = DynamicRealmAccess.GetString(score, "ClientVersion"),
                Hash = DynamicRealmAccess.GetString(score, "Hash"),
                DeletePending = false,
                TotalScore = DynamicRealmAccess.Get<long>(score, "TotalScore"),
                TotalScoreWithoutMods = DynamicRealmAccess.Get<long>(score, "TotalScoreWithoutMods"),
                TotalScoreVersion = DynamicRealmAccess.Get<int>(score, "TotalScoreVersion"),
                LegacyTotalScore = DynamicRealmAccess.Get<long?>(score, "LegacyTotalScore"),
                BackgroundReprocessingFailed = DynamicRealmAccess.Get<bool>(score, "BackgroundReprocessingFailed"),
                MaxCombo = DynamicRealmAccess.Get<int>(score, "MaxCombo"),
                Accuracy = DynamicRealmAccess.Get<double>(score, "Accuracy"),
                Date = DynamicRealmAccess.Get<DateTimeOffset>(score, "Date"),
                PP = DynamicRealmAccess.Get<double?>(score, "PP"),
                OnlineID = DynamicRealmAccess.Get<long>(score, "OnlineID"),
                LegacyOnlineID = DynamicRealmAccess.Get<long>(score, "LegacyOnlineID"),
                User = mapUser(DynamicRealmAccess.Get<IRealmObjectBase>(score, "User")),
                ModsJson = DynamicRealmAccess.GetString(score, "Mods"),
                StatisticsJson = DynamicRealmAccess.GetString(score, "Statistics"),
                MaximumStatisticsJson = DynamicRealmAccess.GetString(score, "MaximumStatistics"),
                RankInt = DynamicRealmAccess.Get<int>(score, "Rank"),
                Combo = DynamicRealmAccess.Get<int>(score, "Combo"),
                IsLegacyScore = DynamicRealmAccess.Get<bool>(score, "IsLegacyScore"),
            };

            foreach (int pause in DynamicRealmAccess.EnumerateValues<int>(score, "Pauses"))
                dto.Pauses.Add(pause);

            foreach (var file in DynamicRealmAccess.EnumerateObjects(score, "Files"))
            {
                OfficialNamedFileDto? usage = mapFileUsage(file);
                if (usage != null)
                    dto.Files.Add(usage);
            }

            return dto;
        }

        private static OfficialBeatmapSetDto mapBeatmapSet(IRealmObjectBase set)
        {
            var dto = new OfficialBeatmapSetDto
            {
                ID = DynamicRealmAccess.Get<Guid>(set, "ID"),
                OnlineID = DynamicRealmAccess.Get<int>(set, "OnlineID"),
                DateAdded = DynamicRealmAccess.Get<DateTimeOffset>(set, "DateAdded"),
                DateSubmitted = DynamicRealmAccess.Get<DateTimeOffset?>(set, "DateSubmitted"),
                DateRanked = DynamicRealmAccess.Get<DateTimeOffset?>(set, "DateRanked"),
                StatusInt = DynamicRealmAccess.Get<int>(set, "Status"),
                DeletePending = false,
                Hash = DynamicRealmAccess.GetString(set, "Hash"),
                Protected = DynamicRealmAccess.Get<bool>(set, "Protected"),
            };

            foreach (var file in DynamicRealmAccess.EnumerateObjects(set, "Files"))
            {
                OfficialNamedFileDto? usage = mapFileUsage(file);
                if (usage != null)
                    dto.Files.Add(usage);
            }

            foreach (var beatmap in DynamicRealmAccess.EnumerateObjects(set, "Beatmaps"))
            {
                if (DynamicRealmAccess.Get<bool>(beatmap, "Hidden"))
                    continue;

                dto.Beatmaps.Add(mapBeatmap(beatmap));
            }

            if (dto.Beatmaps.Count == 0 && set.Realm != null)
            {
                foreach (var beatmap in DynamicRealmAccess.All(set.Realm, OfficialBaselineSchema.Beatmap))
                {
                    var parent = DynamicRealmAccess.Get<IRealmObjectBase>(beatmap, "BeatmapSet");
                    if (parent == null || DynamicRealmAccess.Get<Guid>(parent, "ID") != dto.ID)
                        continue;

                    if (DynamicRealmAccess.Get<bool>(beatmap, "Hidden"))
                        continue;

                    dto.Beatmaps.Add(mapBeatmap(beatmap));
                }
            }

            return dto;
        }

        private static OfficialBeatmapDto mapBeatmap(IRealmObjectBase beatmap)
        {
            var difficulty = DynamicRealmAccess.Get<IRealmObjectBase>(beatmap, "Difficulty");
            var metadata = DynamicRealmAccess.Get<IRealmObjectBase>(beatmap, "Metadata");
            var settings = DynamicRealmAccess.Get<IRealmObjectBase>(beatmap, "UserSettings");
            var ruleset = DynamicRealmAccess.Get<IRealmObjectBase>(beatmap, "Ruleset");
            var parentSet = DynamicRealmAccess.Get<IRealmObjectBase>(beatmap, "BeatmapSet");

            return new OfficialBeatmapDto
            {
                ID = DynamicRealmAccess.Get<Guid>(beatmap, "ID"),
                DifficultyName = DynamicRealmAccess.GetString(beatmap, "DifficultyName"),
                RulesetShortName = DynamicRealmAccess.GetString(ruleset, "ShortName"),
                Difficulty = new OfficialBeatmapDifficultyDto
                {
                    DrainRate = DynamicRealmAccess.Get<float>(difficulty, "DrainRate"),
                    CircleSize = DynamicRealmAccess.Get<float>(difficulty, "CircleSize"),
                    OverallDifficulty = DynamicRealmAccess.Get<float>(difficulty, "OverallDifficulty"),
                    ApproachRate = DynamicRealmAccess.Get<float>(difficulty, "ApproachRate"),
                    SliderMultiplier = DynamicRealmAccess.Get<double>(difficulty, "SliderMultiplier") is double multiplier and > 0 ? multiplier : 1.4,
                    SliderTickRate = DynamicRealmAccess.Get<double>(difficulty, "SliderTickRate") is double tick and > 0 ? tick : 1,
                },
                Metadata = mapMetadata(metadata),
                UserSettings = new OfficialBeatmapUserSettingsDto
                {
                    Offset = DynamicRealmAccess.Get<double>(settings, "Offset"),
                },
                StatusInt = DynamicRealmAccess.Get<int>(beatmap, "Status"),
                OnlineID = DynamicRealmAccess.Get<int>(beatmap, "OnlineID"),
                Length = DynamicRealmAccess.Get<double>(beatmap, "Length"),
                BPM = DynamicRealmAccess.Get<double>(beatmap, "BPM"),
                Hash = DynamicRealmAccess.GetString(beatmap, "Hash"),
                StarRating = DynamicRealmAccess.Get<double>(beatmap, "StarRating"),
                MD5Hash = DynamicRealmAccess.GetString(beatmap, "MD5Hash"),
                OnlineMD5Hash = DynamicRealmAccess.GetString(beatmap, "OnlineMD5Hash"),
                LastLocalUpdate = DynamicRealmAccess.Get<DateTimeOffset?>(beatmap, "LastLocalUpdate"),
                LastOnlineUpdate = DynamicRealmAccess.Get<DateTimeOffset?>(beatmap, "LastOnlineUpdate"),
                Hidden = false,
                EndTimeObjectCount = DynamicRealmAccess.Get<int>(beatmap, "EndTimeObjectCount"),
                TotalObjectCount = DynamicRealmAccess.Get<int>(beatmap, "TotalObjectCount"),
                LastPlayed = DynamicRealmAccess.Get<DateTimeOffset?>(beatmap, "LastPlayed"),
                BeatDivisor = DynamicRealmAccess.Get<int>(beatmap, "BeatDivisor"),
                EditorTimestamp = DynamicRealmAccess.Get<double?>(beatmap, "EditorTimestamp"),
                BeatmapSetID = parentSet == null ? Guid.Empty : DynamicRealmAccess.Get<Guid>(parentSet, "ID"),
            };
        }

        private static OfficialBeatmapMetadataDto mapMetadata(IRealmObjectBase? metadata)
        {
            var dto = new OfficialBeatmapMetadataDto
            {
                Title = DynamicRealmAccess.GetString(metadata, "Title"),
                TitleUnicode = DynamicRealmAccess.GetString(metadata, "TitleUnicode"),
                Artist = DynamicRealmAccess.GetString(metadata, "Artist"),
                ArtistUnicode = DynamicRealmAccess.GetString(metadata, "ArtistUnicode"),
                Author = mapUser(DynamicRealmAccess.Get<IRealmObjectBase>(metadata, "Author")),
                Source = DynamicRealmAccess.GetString(metadata, "Source"),
                Tags = DynamicRealmAccess.GetString(metadata, "Tags"),
                PreviewTime = DynamicRealmAccess.Get<int>(metadata, "PreviewTime"),
                AudioFile = DynamicRealmAccess.GetString(metadata, "AudioFile"),
                BackgroundFile = DynamicRealmAccess.GetString(metadata, "BackgroundFile"),
            };

            if (metadata != null)
            {
                foreach (string tag in DynamicRealmAccess.EnumerateValues<string>(metadata, "UserTags"))
                    dto.UserTags.Add(tag);
            }

            return dto;
        }

        private static OfficialCollectionDto mapCollection(IRealmObjectBase collection)
        {
            var dto = new OfficialCollectionDto
            {
                ID = DynamicRealmAccess.Get<Guid>(collection, "ID"),
                Name = DynamicRealmAccess.GetString(collection, "Name"),
                LastModified = DynamicRealmAccess.Get<DateTimeOffset>(collection, "LastModified"),
            };

            foreach (string md5 in DynamicRealmAccess.EnumerateValues<string>(collection, "BeatmapMD5Hashes"))
                dto.BeatmapMD5Hashes.Add(md5);

            return dto;
        }

        private static OfficialSkinDto? mapSkin(IRealmObjectBase skin)
        {
            Guid id = DynamicRealmAccess.Get<Guid>(skin, "ID");
            string name = DynamicRealmAccess.GetString(skin, "Name");
            string instantiation = DynamicRealmAccess.GetString(skin, "InstantiationInfo");
            if (OfficialBaselineSchema.IsEzOnlySkin(id, instantiation, name))
                return null;

            var dto = new OfficialSkinDto
            {
                ID = id,
                Name = name,
                Creator = DynamicRealmAccess.GetString(skin, "Creator"),
                InstantiationInfo = instantiation,
                Hash = DynamicRealmAccess.GetString(skin, "Hash"),
                Protected = DynamicRealmAccess.Get<bool>(skin, "Protected"),
                DeletePending = false,
            };

            foreach (var file in DynamicRealmAccess.EnumerateObjects(skin, "Files"))
            {
                OfficialNamedFileDto? usage = mapFileUsage(file);
                if (usage != null)
                    dto.Files.Add(usage);
            }

            return dto;
        }

        private static OfficialNamedFileDto? mapFileUsage(IRealmObjectBase usage)
        {
            var file = DynamicRealmAccess.Get<IRealmObjectBase>(usage, "File");
            string hash = DynamicRealmAccess.GetString(file, "Hash");
            if (string.IsNullOrWhiteSpace(hash))
                return null;

            return new OfficialNamedFileDto
            {
                Hash = hash,
                Filename = DynamicRealmAccess.GetString(usage, "Filename"),
            };
        }

        private static OfficialRealmUserDto mapUser(IRealmObjectBase? user) => new OfficialRealmUserDto
        {
            OnlineID = DynamicRealmAccess.Get<int>(user, "OnlineID"),
            Username = DynamicRealmAccess.GetString(user, "Username"),
            CountryString = DynamicRealmAccess.GetString(user, "CountryCode"),
        };

        private static string fingerprint(IEnumerable<string> hashes)
        {
            string[] sorted = hashes.Where(h => !string.IsNullOrEmpty(h)).OrderBy(h => h, StringComparer.Ordinal).ToArray();
            return sorted.Length == 0 ? string.Empty : string.Join('|', sorted);
        }
    }
}
