using osu.Game.EzRealmSync.Contracts;
using osu.Game.EzRealmSync.Models;
using Realms;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 将官方基线 DTO upsert 进目标库：只写白名单列，不改 schema，不碰 Ez 列。
    /// </summary>
    public static class DynamicBaselineWriter
    {
        public static ApplyResult Apply(
            ApplyRequest request,
            RealmSyncApplyBundle bundle,
            string targetRealmPath,
            int targetSchema,
            IProgress<ApplyProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            using var session = DynamicRealmSession.OpenPinned(targetRealmPath, targetSchema, readOnly: false);
            var idSet = request.ItemIds.ToHashSet();
            int applied = 0;

            session.Realm.Write(() =>
            {
                foreach (var set in bundle.BeatmapSets.Where(s => idSet.Contains(s.ID)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    upsertBeatmapSet(session, set);
                    applied++;
                }

                foreach (var beatmap in bundle.Beatmaps.Where(b => idSet.Contains(b.ID)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (upsertStandaloneBeatmap(session, beatmap))
                        applied++;
                }

                foreach (var collection in bundle.Collections.Where(c => idSet.Contains(c.ID)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    upsertCollection(session, collection);
                    applied++;
                }

                foreach (var skin in bundle.Skins.Where(s => idSet.Contains(s.ID)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (upsertSkin(session, skin))
                        applied++;
                }
            });

            progress?.Report(new ApplyProgress { Progress = 1, Message = "写入完成" });
            return new ApplyResult { AppliedCount = applied };
        }

        public static ApplyResult SoftDelete(
            ApplyRequest request,
            string realmFilePath,
            int diskSchemaVersion,
            IProgress<ApplyProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            using var session = DynamicRealmSession.OpenPinned(realmFilePath, diskSchemaVersion, readOnly: false);
            int applied = 0;

            session.Realm.Write(() =>
            {
                foreach (Guid id in request.ItemIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (trySetDeletePending(session, OfficialBaselineSchema.BeatmapSet, id)
                        || trySetDeletePending(session, OfficialBaselineSchema.Skin, id)
                        || trySetDeletePending(session, OfficialBaselineSchema.Score, id))
                    {
                        applied++;
                        continue;
                    }

                    if (session.HasClass(OfficialBaselineSchema.BeatmapCollection)
                        && DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.BeatmapCollection, id) is { } collection)
                    {
                        session.Realm.Remove(collection);
                        applied++;
                    }
                }
            });

            progress?.Report(new ApplyProgress { Progress = 1, Message = "删除完成" });
            return new ApplyResult { AppliedCount = applied };
        }

        private static bool trySetDeletePending(DynamicRealmSession session, string className, Guid id)
        {
            if (!session.HasClass(className))
                return false;

            if (DynamicRealmAccess.Find(session.Realm, className, id) is not { } obj)
                return false;

            DynamicRealmAccess.Set(obj, className, "DeletePending", true);
            return true;
        }

        private static void upsertBeatmapSet(DynamicRealmSession session, OfficialBeatmapSetDto dto)
        {
            if (!session.HasClass(OfficialBaselineSchema.BeatmapSet))
                return;

            var existing = DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.BeatmapSet, dto.ID);
            if (existing != null)
                session.Realm.Remove(existing);

            var set = DynamicRealmAccess.Create(session.Realm, OfficialBaselineSchema.BeatmapSet, dto.ID);
            DynamicRealmAccess.Set(set, OfficialBaselineSchema.BeatmapSet, "OnlineID", dto.OnlineID);
            DynamicRealmAccess.Set(set, OfficialBaselineSchema.BeatmapSet, "DateAdded", dto.DateAdded);
            DynamicRealmAccess.Set(set, OfficialBaselineSchema.BeatmapSet, "DateSubmitted", dto.DateSubmitted);
            DynamicRealmAccess.Set(set, OfficialBaselineSchema.BeatmapSet, "DateRanked", dto.DateRanked);
            DynamicRealmAccess.Set(set, OfficialBaselineSchema.BeatmapSet, "Status", dto.StatusInt);
            DynamicRealmAccess.Set(set, OfficialBaselineSchema.BeatmapSet, "DeletePending", false);
            DynamicRealmAccess.Set(set, OfficialBaselineSchema.BeatmapSet, "Hash", dto.Hash);
            DynamicRealmAccess.Set(set, OfficialBaselineSchema.BeatmapSet, "Protected", dto.Protected);

            linkFiles(session, set, dto.Files);

            foreach (var beatmapDto in dto.Beatmaps)
            {
                if (beatmapDto.Hidden)
                    continue;

                var existingBeatmap = DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.Beatmap, beatmapDto.ID);
                if (existingBeatmap != null)
                    session.Realm.Remove(existingBeatmap);

                var beatmap = createBeatmap(session, beatmapDto);
                DynamicRealmAccess.Set(beatmap, OfficialBaselineSchema.Beatmap, "BeatmapSet", set);
                DynamicRealmAccess.AddToList(DynamicRealmAccess.GetListRaw(set, "Beatmaps"), beatmap);
            }
        }

        private static bool upsertStandaloneBeatmap(DynamicRealmSession session, OfficialBeatmapDto dto)
        {
            if (!session.HasClass(OfficialBaselineSchema.Beatmap))
                return false;

            var existing = DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.Beatmap, dto.ID);
            if (existing != null)
            {
                var parent = DynamicRealmAccess.Get<IRealmObjectBase>(existing, "BeatmapSet");
                writeBeatmapFields(session, existing, dto);
                if (parent != null)
                    DynamicRealmAccess.Set(existing, OfficialBaselineSchema.Beatmap, "BeatmapSet", parent);
                return true;
            }

            return false;
        }

        private static IRealmObjectBase createBeatmap(DynamicRealmSession session, OfficialBeatmapDto dto)
        {
            var beatmap = DynamicRealmAccess.Create(session.Realm, OfficialBaselineSchema.Beatmap, dto.ID);
            writeBeatmapFields(session, beatmap, dto);
            return beatmap;
        }

        private static void writeBeatmapFields(DynamicRealmSession session, IRealmObjectBase beatmap, OfficialBeatmapDto dto)
        {
            DynamicRealmAccess.Set(beatmap, OfficialBaselineSchema.Beatmap, "DifficultyName", dto.DifficultyName);
            DynamicRealmAccess.Set(beatmap, OfficialBaselineSchema.Beatmap, "Status", dto.StatusInt);
            DynamicRealmAccess.Set(beatmap, OfficialBaselineSchema.Beatmap, "OnlineID", dto.OnlineID);
            DynamicRealmAccess.Set(beatmap, OfficialBaselineSchema.Beatmap, "Length", dto.Length);
            DynamicRealmAccess.Set(beatmap, OfficialBaselineSchema.Beatmap, "BPM", dto.BPM);
            DynamicRealmAccess.Set(beatmap, OfficialBaselineSchema.Beatmap, "Hash", dto.Hash);
            DynamicRealmAccess.Set(beatmap, OfficialBaselineSchema.Beatmap, "StarRating", dto.StarRating);
            DynamicRealmAccess.Set(beatmap, OfficialBaselineSchema.Beatmap, "MD5Hash", dto.MD5Hash);
            DynamicRealmAccess.Set(beatmap, OfficialBaselineSchema.Beatmap, "OnlineMD5Hash", dto.OnlineMD5Hash);
            DynamicRealmAccess.Set(beatmap, OfficialBaselineSchema.Beatmap, "LastLocalUpdate", dto.LastLocalUpdate);
            DynamicRealmAccess.Set(beatmap, OfficialBaselineSchema.Beatmap, "LastOnlineUpdate", dto.LastOnlineUpdate);
            DynamicRealmAccess.Set(beatmap, OfficialBaselineSchema.Beatmap, "Hidden", false);
            DynamicRealmAccess.Set(beatmap, OfficialBaselineSchema.Beatmap, "EndTimeObjectCount", dto.EndTimeObjectCount);
            DynamicRealmAccess.Set(beatmap, OfficialBaselineSchema.Beatmap, "TotalObjectCount", dto.TotalObjectCount);
            DynamicRealmAccess.Set(beatmap, OfficialBaselineSchema.Beatmap, "LastPlayed", dto.LastPlayed);
            DynamicRealmAccess.Set(beatmap, OfficialBaselineSchema.Beatmap, "BeatDivisor", dto.BeatDivisor);
            DynamicRealmAccess.Set(beatmap, OfficialBaselineSchema.Beatmap, "EditorTimestamp", dto.EditorTimestamp);
            DynamicRealmAccess.Set(beatmap, OfficialBaselineSchema.Beatmap, "Ruleset", resolveRuleset(session, dto.RulesetShortName));

            var difficulty = createChild(session, beatmap, "Difficulty", OfficialBaselineSchema.BeatmapDifficulty);
            DynamicRealmAccess.Set(difficulty, OfficialBaselineSchema.BeatmapDifficulty, "DrainRate", dto.Difficulty.DrainRate);
            DynamicRealmAccess.Set(difficulty, OfficialBaselineSchema.BeatmapDifficulty, "CircleSize", dto.Difficulty.CircleSize);
            DynamicRealmAccess.Set(difficulty, OfficialBaselineSchema.BeatmapDifficulty, "OverallDifficulty", dto.Difficulty.OverallDifficulty);
            DynamicRealmAccess.Set(difficulty, OfficialBaselineSchema.BeatmapDifficulty, "ApproachRate", dto.Difficulty.ApproachRate);
            DynamicRealmAccess.Set(difficulty, OfficialBaselineSchema.BeatmapDifficulty, "SliderMultiplier", dto.Difficulty.SliderMultiplier);
            DynamicRealmAccess.Set(difficulty, OfficialBaselineSchema.BeatmapDifficulty, "SliderTickRate", dto.Difficulty.SliderTickRate);

            var metadata = createChild(session, beatmap, "Metadata", OfficialBaselineSchema.BeatmapMetadata);
            DynamicRealmAccess.Set(metadata, OfficialBaselineSchema.BeatmapMetadata, "Title", dto.Metadata.Title);
            DynamicRealmAccess.Set(metadata, OfficialBaselineSchema.BeatmapMetadata, "TitleUnicode", dto.Metadata.TitleUnicode);
            DynamicRealmAccess.Set(metadata, OfficialBaselineSchema.BeatmapMetadata, "Artist", dto.Metadata.Artist);
            DynamicRealmAccess.Set(metadata, OfficialBaselineSchema.BeatmapMetadata, "ArtistUnicode", dto.Metadata.ArtistUnicode);
            DynamicRealmAccess.Set(metadata, OfficialBaselineSchema.BeatmapMetadata, "Source", dto.Metadata.Source);
            DynamicRealmAccess.Set(metadata, OfficialBaselineSchema.BeatmapMetadata, "Tags", dto.Metadata.Tags);
            DynamicRealmAccess.Set(metadata, OfficialBaselineSchema.BeatmapMetadata, "PreviewTime", dto.Metadata.PreviewTime);
            DynamicRealmAccess.Set(metadata, OfficialBaselineSchema.BeatmapMetadata, "AudioFile", dto.Metadata.AudioFile);
            DynamicRealmAccess.Set(metadata, OfficialBaselineSchema.BeatmapMetadata, "BackgroundFile", dto.Metadata.BackgroundFile);

            var author = createChild(session, metadata, "Author", OfficialBaselineSchema.RealmUser);
            writeUser(author, dto.Metadata.Author);

            object? userTags = DynamicRealmAccess.GetListRaw(metadata, "UserTags");
            DynamicRealmAccess.ClearList(userTags);
            foreach (string tag in dto.Metadata.UserTags)
                DynamicRealmAccess.AddToList(userTags, tag);

            var settings = createChild(session, beatmap, "UserSettings", OfficialBaselineSchema.BeatmapUserSettings);
            DynamicRealmAccess.Set(settings, OfficialBaselineSchema.BeatmapUserSettings, "Offset", dto.UserSettings.Offset);
        }

        private static void upsertCollection(DynamicRealmSession session, OfficialCollectionDto dto)
        {
            if (!session.HasClass(OfficialBaselineSchema.BeatmapCollection))
                return;

            var existing = DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.BeatmapCollection, dto.ID);
            if (existing != null)
                session.Realm.Remove(existing);

            var collection = DynamicRealmAccess.Create(session.Realm, OfficialBaselineSchema.BeatmapCollection, dto.ID);
            DynamicRealmAccess.Set(collection, OfficialBaselineSchema.BeatmapCollection, "Name", dto.Name);
            DynamicRealmAccess.Set(collection, OfficialBaselineSchema.BeatmapCollection, "LastModified", dto.LastModified);

            object? hashes = DynamicRealmAccess.GetListRaw(collection, "BeatmapMD5Hashes");
            DynamicRealmAccess.ClearList(hashes);
            foreach (string md5 in dto.BeatmapMD5Hashes)
                DynamicRealmAccess.AddToList(hashes, md5);
        }

        private static bool upsertSkin(DynamicRealmSession session, OfficialSkinDto dto)
        {
            if (!session.HasClass(OfficialBaselineSchema.Skin))
                return false;

            if (OfficialBaselineSchema.IsEzOnlySkin(dto.ID, dto.InstantiationInfo, dto.Name))
                return false;

            var existing = DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.Skin, dto.ID);
            if (existing != null)
                session.Realm.Remove(existing);

            var skin = DynamicRealmAccess.Create(session.Realm, OfficialBaselineSchema.Skin, dto.ID);
            DynamicRealmAccess.Set(skin, OfficialBaselineSchema.Skin, "Name", dto.Name);
            DynamicRealmAccess.Set(skin, OfficialBaselineSchema.Skin, "Creator", dto.Creator);
            DynamicRealmAccess.Set(skin, OfficialBaselineSchema.Skin, "InstantiationInfo", dto.InstantiationInfo);
            DynamicRealmAccess.Set(skin, OfficialBaselineSchema.Skin, "Hash", dto.Hash);
            DynamicRealmAccess.Set(skin, OfficialBaselineSchema.Skin, "Protected", dto.Protected);
            DynamicRealmAccess.Set(skin, OfficialBaselineSchema.Skin, "DeletePending", false);
            linkFiles(session, skin, dto.Files);
            return true;
        }

        private static IRealmObjectBase createChild(DynamicRealmSession session, IRealmObjectBase parent, string property, string className)
        {
            try
            {
                return session.Realm.DynamicApi.CreateEmbeddedObjectForProperty(parent, property);
            }
            catch (ArgumentException)
            {
                var child = DynamicRealmAccess.Create(session.Realm, className);
                string parentClass = parent.ObjectSchema?.Name ?? OfficialBaselineSchema.Beatmap;
                DynamicRealmAccess.Set(parent, parentClass, property, child);
                return child;
            }
        }

        private static IRealmObjectBase resolveRuleset(DynamicRealmSession session, string shortName)
        {
            if (string.IsNullOrWhiteSpace(shortName))
                shortName = "osu";

            var existing = DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.Ruleset, shortName);
            if (existing != null)
                return existing;

            var created = DynamicRealmAccess.Create(session.Realm, OfficialBaselineSchema.Ruleset, shortName);
            DynamicRealmAccess.Set(created, OfficialBaselineSchema.Ruleset, "Name", shortName);
            DynamicRealmAccess.Set(created, OfficialBaselineSchema.Ruleset, "InstantiationInfo", string.Empty);
            DynamicRealmAccess.Set(created, OfficialBaselineSchema.Ruleset, "Available", true);
            return created;
        }

        private static void linkFiles(DynamicRealmSession session, IRealmObjectBase owner, IReadOnlyList<OfficialNamedFileDto> files)
        {
            if (!session.HasClass(OfficialBaselineSchema.File))
                return;

            object? list = DynamicRealmAccess.GetListRaw(owner, "Files");
            DynamicRealmAccess.ClearList(list);

            foreach (var usageDto in files)
            {
                if (string.IsNullOrWhiteSpace(usageDto.Hash))
                    continue;

                var file = DynamicRealmAccess.Find(session.Realm, OfficialBaselineSchema.File, usageDto.Hash)
                           ?? DynamicRealmAccess.Create(session.Realm, OfficialBaselineSchema.File, usageDto.Hash);

                if (list == null)
                    continue;

                var usage = session.Realm.DynamicApi.AddEmbeddedObjectToList(list);
                DynamicRealmAccess.Set(usage, OfficialBaselineSchema.NamedFileUsage, "File", file);
                DynamicRealmAccess.Set(usage, OfficialBaselineSchema.NamedFileUsage, "Filename", usageDto.Filename);
            }
        }

        private static void writeUser(IRealmObjectBase user, OfficialRealmUserDto dto)
        {
            DynamicRealmAccess.Set(user, OfficialBaselineSchema.RealmUser, "OnlineID", dto.OnlineID);
            DynamicRealmAccess.Set(user, OfficialBaselineSchema.RealmUser, "Username", dto.Username);
            DynamicRealmAccess.Set(user, OfficialBaselineSchema.RealmUser, "CountryCode", dto.CountryString);
        }
    }
}
