namespace osu.Game.EzRealmSync.Realm.Readers
{
    internal static class RealmOpenErrorClassifier
    {
        public static bool IsMigrationRequired(Exception ex)
        {
            for (Exception? current = ex; current != null; current = current.InnerException)
            {
                if (current.Message.Contains("Migration is required", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
