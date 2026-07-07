namespace osu.Game.EzRealmSync.Realm.Readers
{
    public static class RealmReaderPaths
    {
        public static string DefaultPackagesDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EzRealmSync",
            "readers");

        public static string ResolvePackagesDirectory(string? configuredDirectory)
        {
            if (!string.IsNullOrWhiteSpace(configuredDirectory))
                return Path.GetFullPath(configuredDirectory);

            return DefaultPackagesDirectory;
        }
    }
}
