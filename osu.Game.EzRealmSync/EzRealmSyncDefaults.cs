namespace osu.Game.EzRealmSync
{
    public static class EzRealmSyncDefaults
    {
        public static string DefaultBackupDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EzRealmSync", "backups");
    }
}
