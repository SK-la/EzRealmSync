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

        public static string OfficialSharedLibDirectory(string? packagesDirectory = null) =>
            Path.Combine(ResolvePackagesDirectory(packagesDirectory), "_shared", "official", "lib");

        public static string? ResolveSharedLibDirectory(string profile, string? packagesDirectory = null)
        {
            if (string.Equals(profile, "official", StringComparison.OrdinalIgnoreCase))
            {
                string official = OfficialSharedLibDirectory(packagesDirectory);
                return Directory.Exists(official) ? official : null;
            }

            return EzRealmSyncBackend.ResolveRuntimeLibDirectory();
        }

        public static bool HasOfficialSharedBaseline(string? packagesDirectory = null) =>
            File.Exists(Path.Combine(OfficialSharedLibDirectory(packagesDirectory), "Realm.dll"));
    }
}
