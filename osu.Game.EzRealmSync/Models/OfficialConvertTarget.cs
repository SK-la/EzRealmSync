namespace osu.Game.EzRealmSync.Models
{
    /// <summary>转回官方时的目标 upstream 策略。</summary>
    public enum OfficialConvertTarget
    {
        /// <summary>保持文件头解码出的官方 upstream（剥 Ez，不升到 lib）。</summary>
        PreserveReadUpstream,

        /// <summary>升到 bundled osu.Game 的 <see cref="osu.Game.Database.RealmAccess.UpstreamSchemaVersion"/>。</summary>
        UpgradeToLibUpstream,
    }
}
