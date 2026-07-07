#if HAS_EZ_OSU_GAME
namespace osu.Game.EzRealmSync.Realm.Readers
{
    public sealed class RealmReaderRegistry
    {
        public static RealmReaderRegistry Instance { get; } = new();

        private IReadOnlyList<RealmReaderPackageInfo> packages = Array.Empty<RealmReaderPackageInfo>();
        private RealmReaderRouter router = new();
        private bool initialized;

        private RealmReaderRegistry()
        {
        }

        public RealmReaderRouter Router
        {
            get
            {
                ensureInitialized();
                return router;
            }
        }

        public IReadOnlyList<RealmReaderPackageInfo> Packages
        {
            get
            {
                ensureInitialized();
                return packages;
            }
        }

        public string PackagesDirectory { get; private set; } = RealmReaderPaths.DefaultPackagesDirectory;

        public void Initialize(string? packagesDirectory = null)
        {
            PackagesDirectory = RealmReaderPaths.ResolvePackagesDirectory(packagesDirectory);
            packages = RealmReaderPackageCatalog.Scan(PackagesDirectory);
            router = new RealmReaderRouter();
            initialized = true;
        }

        private void ensureInitialized()
        {
            if (!initialized)
                Initialize();
        }

        public RealmReaderPackageInfo? FindPackageForSchema(int diskSchemaVersion) =>
            RealmReaderPackageCatalog.FindForSchema(packages, diskSchemaVersion);

        public RealmReaderPackageInfo? FindPackageById(string? packageId) =>
            RealmReaderPackageCatalog.FindById(packages, packageId);
    }
}
#endif
