namespace osu.Game.EzRealmSync.Realm.Readers
{
    public static class RealmReaderPaths
    {
        public static string DefaultPackagesDirectory => EzRealmSyncDataPaths.ReadersDirectory;

        public static string ResolvePackagesDirectory(string? configuredDirectory)
        {
            if (!string.IsNullOrWhiteSpace(configuredDirectory))
                return Path.GetFullPath(configuredDirectory);

            return DefaultPackagesDirectory;
        }
    }
}
