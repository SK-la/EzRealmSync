#if HAS_EZ_OSU_GAME
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.EzRealmSync.Models;
using osu.Game.Scoring;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 在单个 Realm 库内按 ID 软删（或移除收藏夹）；不触碰共享 <c>files/</c> blob。
    /// </summary>
    public static class RealmSyncDeleter
    {
        public static ApplyResult Apply(
            ApplyRequest request,
            RealmAccess targetAccess,
            IProgress<ApplyProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            string? validationError = RealmApplySupport.ValidateApplyRequest(request);
            if (validationError != null)
                throw new InvalidOperationException(validationError);

            if (!request.DeleteFromSource)
                throw new InvalidOperationException("RealmSyncDeleter 仅用于 DeleteFromSource。");

            int applied = 0;
            int total = request.ItemIds.Count;

            for (int i = 0; i < request.ItemIds.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Guid id = request.ItemIds[i];

                progress?.Report(new ApplyProgress
                {
                    Progress = total == 0 ? 1 : (i + 1) / (double)total,
                    Message = $"正在删除 {i + 1}/{total}…",
                });

                if (tryDelete(id, targetAccess))
                    applied++;
            }

            progress?.Report(new ApplyProgress { Progress = 1, Message = "删除完成" });

            return new ApplyResult { AppliedCount = applied };
        }

        internal static bool tryDelete(Guid id, RealmAccess access)
        {
            bool deleted = false;

            access.Write(realm =>
            {
                if (realm.Find<BeatmapSetInfo>(id) is BeatmapSetInfo set)
                {
                    set.DeletePending = true;
                    deleted = true;
                    return;
                }

                if (realm.Find<ScoreInfo>(id) is ScoreInfo score)
                {
                    score.DeletePending = true;
                    deleted = true;
                    return;
                }

                if (realm.Find<BeatmapCollection>(id) is BeatmapCollection collection)
                {
                    realm.Remove(collection);
                    deleted = true;
                }
            });

            return deleted;
        }
    }
}
#endif
