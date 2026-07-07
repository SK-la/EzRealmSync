using System.Collections.ObjectModel;
using osu.EzRealmSync.AppModel.Localization;
using osu.Framework.Bindables;
using osu.Game.EzRealmSync;
using osu.Game.EzRealmSync.Abstractions;
using osu.Game.EzRealmSync.Mock;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.AppModel
{
    public enum MainWorkspaceTab
    {
        Import,
        Data,
        Sync,
        Fix,
        Export,
    }

    public sealed class RealmAppPresenter
    {
        private readonly RealmServiceHost services;
        private readonly EzRealmSyncLaunchOptions launchOptions;

        private IEzRealmSyncService syncService => services.Sync;
        private IRealmDataService dataService => services.Data;
        private IRealmFixService fixService => services.Fix;
        private IRealmExportService exportService => services.Export;

        private ScanResult? lastScanResult;
        private CancellationTokenSource? operationCts;
        private readonly List<DiffRowModel> syncRows = new();
        private RealmSnapshot? loadedSnapshot;
        private readonly Dictionary<RealmObjectClass, List<RealmBrowseRow>> browseRowsByClass = new();
        private readonly bool loadingSettings;
        private bool suppressBackendModeChange;

        public RealmAppPresenter(RealmServiceHost services, EzRealmSyncLaunchOptions launchOptions)
        {
            this.services = services;
            this.launchOptions = launchOptions;

            loadingSettings = true;
            var settings = AppSettingsStore.Load();
            applySettings(settings);

            bool initialUiTest = launchOptions.HasUiTestModeArgument
                ? launchOptions.UiTestMode
                : settings.UiTestMode;

            services.SetUiTestMode(initialUiTest, force: true);
            UiTestMode.Value = initialUiTest;
            BackendKind = services.BackendKind;
            StatusMessage.Value = resolveBackendStatusMessage();
            loadingSettings = false;

            UiTestMode.BindValueChanged(mode =>
            {
                if (loadingSettings || suppressBackendModeChange)
                    return;

                applyBackendMode(mode.NewValue);
            });

            UiTestMode.BindValueChanged(_ => persistSettings());

            CurrentWorkspaceTab.BindValueChanged(_ => { }, true);
            EntityFilter.BindValueChanged(_ => refreshSyncRows(), true);
            CurrentCategory.BindValueChanged(_ =>
            {
                refreshSyncRows();
                updateCanApply();
            }, true);
            SelectedRealmClass.BindValueChanged(_ => refreshBrowseTable(), true);
            SyncAction.BindValueChanged(_ => updateCanApply(), true);

            SearchDirectory.BindValueChanged(_ => persistSettings());
            BackupDirectory.BindValueChanged(_ => persistSettings());
            ImportSelectedRealmId.BindValueChanged(_ => persistSettings());
            DataRealmId.BindValueChanged(_ => persistSettings());
            SyncRealmIdA.BindValueChanged(_ => persistSettings());
            SyncRealmIdB.BindValueChanged(_ => persistSettings());
            FixRealmId.BindValueChanged(_ => persistSettings());
            ExportRealmId.BindValueChanged(_ =>
            {
                persistSettings();
                exportService.InvalidateCatalog(ExportRealmId.Value);
                runOnUi(ClearExportItems);
            });
            ExportDirectory.BindValueChanged(_ => persistSettings());
            ExportFolderName.BindValueChanged(_ => persistSettings());
            ExportGroupScoresByPlayer.BindValueChanged(_ => persistSettings());
            IllegalCharacterReplacement.BindValueChanged(_ => persistSettings());
            ConfirmBeforeDelete.BindValueChanged(_ => persistSettings());
        }

        public ObservableCollection<RealmFileEntry> RealmFiles { get; } = new();

        public ObservableCollection<RealmFileRowModel> RealmFileRows { get; } = new();

        public ObservableCollection<RealmBrowseRowModel> BrowseRows { get; } = new();

        public ObservableCollection<RealmEntityRowModel> DataRows { get; } = new();

        public ObservableCollection<RealmClassListItemModel> DataClasses { get; } = new();

        public ObservableCollection<RealmFixIssueModel> FixIssues { get; } = new();

        public ObservableCollection<RealmExportItemModel> ExportItems { get; } = new();

        public ObservableCollection<BackupEntryRowModel> BackupEntries { get; } = new();

        public Bindable<string?> SelectedBackupId { get; } = new Bindable<string?>();

        public Bindable<MainWorkspaceTab> CurrentWorkspaceTab { get; } = new Bindable<MainWorkspaceTab>(MainWorkspaceTab.Import);

        public Bindable<string> BackupDirectory { get; } = new Bindable<string>(string.Empty);

        /// <summary>导入页：扫描该路径下所有 <c>*.realm</c>。</summary>
        public Bindable<string> SearchDirectory { get; } = new Bindable<string>(string.Empty);

        public BindableBool CanUseFixAndExport { get; } = new BindableBool();

        public Bindable<string?> ImportSelectedRealmId { get; } = new Bindable<string?>();

        public Bindable<string?> DataRealmId { get; } = new Bindable<string?>();

        public Bindable<string?> SyncRealmIdA { get; } = new Bindable<string?>();
        public Bindable<string?> SyncRealmIdB { get; } = new Bindable<string?>();

        public Bindable<string?> FixRealmId { get; } = new Bindable<string?>();

        public Bindable<string?> ExportRealmId { get; } = new Bindable<string?>();

        public Bindable<ExportDataKind> SelectedExportKind { get; } = new Bindable<ExportDataKind>(ExportDataKind.BeatmapSet);

        public Bindable<string> IllegalCharacterReplacement { get; } = new Bindable<string>("_");

        public BindableBool ConfirmBeforeDelete { get; } = new BindableBool(true);

        public Bindable<string> ExportDirectory { get; } = new Bindable<string>(string.Empty);

        public Bindable<string> ExportFolderName { get; } = new Bindable<string>(string.Empty);

        public BindableBool ExportGroupScoresByPlayer { get; } = new BindableBool(true);

        public Bindable<EntityKind> SelectedDataGroup { get; } = new Bindable<EntityKind>(EntityKind.BeatmapSet);

        public Bindable<RealmObjectClass> SelectedRealmClass { get; } = new Bindable<RealmObjectClass>(RealmObjectClass.BeatmapSet);
        public Bindable<RealmSetOperation> SetOperation { get; } = new Bindable<RealmSetOperation>(RealmSetOperation.Difference);
        public Bindable<RealmSyncAction> SyncAction { get; } = new Bindable<RealmSyncAction>(RealmSyncAction.Add);

        public BindableBool UiTestMode { get; } = new BindableBool();

        public EzRealmSyncBackendKind BackendKind { get; private set; }

        public Bindable<EntityKindFilter> EntityFilter { get; } = new Bindable<EntityKindFilter>(EntityKindFilter.All);
        public Bindable<DiffCategory> CurrentCategory { get; } = new Bindable<DiffCategory>(DiffCategory.SourceOnly);

        public Bindable<string> StatusMessage { get; } = new Bindable<string>(string.Empty);
        public Bindable<double> Progress { get; } = new Bindable<double>();
        public BindableInt SelectionCount { get; } = new BindableInt();
        public BindableInt DataSelectionCount { get; } = new BindableInt();
        public BindableBool IsBusy { get; } = new BindableBool();
        public BindableBool CanApply { get; } = new BindableBool(true);
        public BindableBool IsSelectAllMode { get; } = new BindableBool(true);

        public string LoadedSnapshotSummary { get; private set; } = string.Empty;

        public IReadOnlyList<RealmColumnDefinition> BrowseColumns { get; private set; } = Array.Empty<RealmColumnDefinition>();

        public IReadOnlyList<DiffRowModel> SyncRows => syncRows;

        public MockEzRealmSyncService? MockService => syncService as MockEzRealmSyncService;

        public Func<string, string, bool, Task<bool>>? ConfirmAsync { get; set; }
        public Func<string, Task<string?>>? PickFolderAsync { get; set; }
        public Func<string, Task<string?>>? PickRealmPathAsync { get; set; }

        /// <summary>由 Desktop 注入，将集合/绑定更新封送到 UI 线程。</summary>
        public Action<Action>? MarshalToUi { get; set; }

        public event Action? RealmFilesChanged;
        public event Action? BackupEntriesChanged;
        public event Action? SyncRowsChanged;
        public event Action? DataRowsChanged;
        public event Action? BrowseTableChanged;
        public event Action? DataClassesChanged;
        public event Action? FixIssuesChanged;
        public event Action? ExportItemsChanged;
        public event Action? WorkspaceCapabilitiesChanged;
        public event Action? LabelsChanged;

        public async Task InitializeAsync()
        {
            var defaults = launchOptions.CreateDefaultPaths();

            if (string.IsNullOrWhiteSpace(SearchDirectory.Value) && !string.IsNullOrWhiteSpace(defaults.EzDataPath))
            {
                SearchDirectory.Value = defaults.EzDataPath;
                persistSettings();
            }

            await RefreshRealmFilesAsync().ConfigureAwait(false);

            runOnUi(() => updateWorkspaceCapabilities());
        }

        public async Task RefreshRealmFilesAsync()
        {
            setBusy(true);

            try
            {
                string? searchDirectory = resolveSearchDirectoryForDiscovery();
                var files = await discoverRealmFilesAsync(searchDirectory).ConfigureAwait(false);

                runOnUi(() =>
                {
                    RealmFiles.Clear();
                    foreach (var file in files)
                        RealmFiles.Add(file);

                    reconcileTabRealmSelections();
                    refreshRealmFileRows();
                    RealmFilesChanged?.Invoke();
                    updateWorkspaceCapabilities();
                    StatusMessage.Value = buildRealmListStatusMessage(searchDirectory, files.Count);
                });
            }
            catch (Exception ex)
            {
                runOnUi(() => StatusMessage.Value = ex.Message);
            }
            finally
            {
                setBusy(false);
            }
        }

        private string? resolveSearchDirectoryForDiscovery()
        {
            if (string.IsNullOrWhiteSpace(SearchDirectory.Value))
                return null;

            string normalized = RealmWorkspaceDiscovery.NormalizeStorageRoot(SearchDirectory.Value);

            if (!string.Equals(normalized, SearchDirectory.Value.Trim(), StringComparison.OrdinalIgnoreCase))
                runOnUi(() => SearchDirectory.Value = normalized);

            return normalized;
        }

        private async Task<IReadOnlyList<RealmFileEntry>> discoverRealmFilesAsync(string? searchDirectory, CancellationToken cancellationToken = default)
        {
            var files = (await dataService.DiscoverRealmFilesAsync(searchDirectory, cancellationToken).ConfigureAwait(false)).ToList();

            if (files.Count > 0 || string.IsNullOrWhiteSpace(searchDirectory))
                return files;

            var diskPaths = RealmWorkspaceDiscovery.FindRealmFilesInSearchDirectory(searchDirectory);
            if (diskPaths.Count == 0)
                return files;

            var recovered = new List<RealmFileEntry>();

            foreach (string path in diskPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    recovered.Add(await dataService.RegisterRealmFileAsync(path, cancellationToken).ConfigureAwait(false));
                }
                catch
                {
                    if (RealmFileDiscovery.TryCreateEntry(path, schemaVersion: null, out var entry))
                        recovered.Add(entry);
                }
            }

            return recovered
                   .GroupBy(f => f.FilePath, StringComparer.OrdinalIgnoreCase)
                   .Select(g => g.First())
                   .OrderBy(f => f.DisplayName, StringComparer.OrdinalIgnoreCase)
                   .ToList();
        }

        private string buildRealmListStatusMessage(string? searchDirectory, int count)
        {
            if (count == 0)
            {
                if (string.IsNullOrWhiteSpace(searchDirectory))
                    return Loc.Get("StatusSetSearchDirectory");

                if (RealmWorkspaceDiscovery.FindRealmFilesInSearchDirectory(searchDirectory).Count == 0)
                    return Loc.Format("StatusNoRealmInPath", searchDirectory);

                if (BackendKind == EzRealmSyncBackendKind.Stub)
                    return resolveBackendStatusMessage();

                return Loc.Format("StatusNoRealmRegistered", searchDirectory);
            }

            if (!string.IsNullOrWhiteSpace(searchDirectory)
                && RealmWorkspaceDiscovery.TryResolveSharedFilesDirectory(searchDirectory, out string sharedFiles))
            {
                return Loc.Format("StatusStorageReady", count, sharedFiles);
            }

            return Loc.Format("StatusRealmList", count);
        }

        public Task ApplySearchDirectoryAsync() => applySearchDirectoryAsync();

        private async Task applySearchDirectoryAsync()
        {
            string path = SearchDirectory.Value.Trim();
            if (string.IsNullOrEmpty(path))
                return;

            string normalized = RealmWorkspaceDiscovery.NormalizeStorageRoot(path);

            if (File.Exists(path) && path.EndsWith(".realm", StringComparison.OrdinalIgnoreCase)
                || Directory.Exists(normalized))
            {
                runOnUi(() => SearchDirectory.Value = normalized);
                await RefreshRealmFilesAsync().ConfigureAwait(false);
                return;
            }

            runOnUi(() => StatusMessage.Value = Loc.Get("StatusInvalidRealmPath"));
        }

        public async Task RegisterRealmFileAsync(string realmFilePath)
        {
            setBusy(true);

            try
            {
                var entry = await dataService.RegisterRealmFileAsync(realmFilePath).ConfigureAwait(false);
                await RefreshRealmFilesAsync().ConfigureAwait(false);
                runOnUi(() =>
                {
                    if (string.IsNullOrWhiteSpace(SearchDirectory.Value))
                        SearchDirectory.Value = RealmWorkspaceDiscovery.NormalizeStorageRoot(realmFilePath);

                    ImportSelectedRealmId.Value = entry.Id;
                    StatusMessage.Value = Loc.Format("StatusRealmRegistered", entry.DisplayName);
                });
            }
            catch (Exception ex)
            {
                runOnUi(() => StatusMessage.Value = ex.Message);
            }
            finally
            {
                setBusy(false);
            }
        }

        public async Task BrowseBackupDirectoryAsync()
        {
            if (PickFolderAsync == null)
                return;

            string? picked = await PickFolderAsync(BackupDirectory.Value).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(picked))
                runOnUi(() => BackupDirectory.Value = picked);
        }

        public Task BrowseSearchDirectoryAsync() => browseSearchDirectoryAsync();

        private async Task browseSearchDirectoryAsync()
        {
            if (PickFolderAsync == null)
                return;

            string? picked = await PickFolderAsync(SearchDirectory.Value).ConfigureAwait(false);
            if (string.IsNullOrEmpty(picked))
                return;

            runOnUi(() => SearchDirectory.Value = RealmWorkspaceDiscovery.NormalizeStorageRoot(picked));
            await RefreshRealmFilesAsync().ConfigureAwait(false);
        }

        public async Task BackupSelectedRealmAsync()
        {
            var file = getRealmFile(ImportSelectedRealmId.Value);
            if (file == null)
                return;

            setBusy(true);

            try
            {
                string backupPath = await dataService.CreateTimestampedBackupAsync(file.FilePath, BackupDirectory.Value).ConfigureAwait(false);
                runOnUi(() => StatusMessage.Value = Loc.Format("StatusBackupCreated", backupPath));
                await RefreshBackupsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                runOnUi(() => StatusMessage.Value = ex.Message);
            }
            finally
            {
                setBusy(false);
            }
        }

        public async Task RefreshBackupsAsync()
        {
            setBusy(true);

            try
            {
                var backups = await syncService.ListBackupsAsync(BackupDirectory.Value).ConfigureAwait(false);

                runOnUi(() =>
                {
                    BackupEntries.Clear();
                    foreach (var entry in backups)
                        BackupEntries.Add(new BackupEntryRowModel(entry));

                    if (BackupEntries.Count > 0 && BackupEntries.All(b => b.Id != SelectedBackupId.Value))
                        SelectedBackupId.Value = BackupEntries[0].Id;

                    StatusMessage.Value = Loc.Format("StatusBackupsListed", BackupEntries.Count);
                    BackupEntriesChanged?.Invoke();
                });
            }
            catch (Exception ex)
            {
                runOnUi(() => StatusMessage.Value = ex.Message);
            }
            finally
            {
                setBusy(false);
            }
        }

        public async Task RestoreSelectedBackupAsync()
        {
            var file = getRealmFile(ImportSelectedRealmId.Value);

            if (file == null)
            {
                runOnUi(() => StatusMessage.Value = Loc.Get("ErrorPickRealmForRestore"));
                return;
            }

            if (string.IsNullOrEmpty(SelectedBackupId.Value))
            {
                runOnUi(() => StatusMessage.Value = Loc.Get("ErrorPickBackup"));
                return;
            }

            if (ConfirmAsync != null)
            {
                bool ok = await ConfirmAsync(Loc.Format("ConfirmRestore", file.DisplayName), Loc.Get("ConfirmRestoreTitle"), true).ConfigureAwait(false);
                if (!ok)
                    return;
            }

            setBusy(true);

            try
            {
                var progress = createApplyProgress();
                await syncService.RestoreBackupAsync(
                    SelectedBackupId.Value,
                    file.FilePath,
                    BackupDirectory.Value,
                    BackupDirectory.Value,
                    progress).ConfigureAwait(false);

                runOnUi(() => StatusMessage.Value = Loc.Format("StatusBackupRestored", file.DisplayName));
            }
            catch (Exception ex)
            {
                runOnUi(() => StatusMessage.Value = ex.Message);
            }
            finally
            {
                setBusy(false);
            }
        }

        public async Task LoadSelectedRealmAsync()
        {
            if (string.IsNullOrEmpty(DataRealmId.Value))
                return;

            setBusy(true);

            try
            {
                var progress = createScanProgress();
                var snapshot = await dataService.LoadRealmSnapshotAsync(DataRealmId.Value, progress).ConfigureAwait(false);

                runOnUi(() =>
                {
                    loadedSnapshot = snapshot;
                    LoadedSnapshotSummary = Loc.Format("StatusRealmLoaded", loadedSnapshot.DisplayName, loadedSnapshot.TotalRowCount);
                    StatusMessage.Value = LoadedSnapshotSummary;
                    Progress.Value = 1;
                    refreshDataBrowse();
                });
            }
            catch (Exception ex)
            {
                runOnUi(() => StatusMessage.Value = ex.Message);
            }
            finally
            {
                setBusy(false);
            }
        }

        public async Task ComputeSetAsync()
        {
            if (string.IsNullOrEmpty(SyncRealmIdA.Value) || string.IsNullOrEmpty(SyncRealmIdB.Value))
            {
                runOnUi(() => StatusMessage.Value = Loc.Get("ErrorPickSourceTarget"));
                return;
            }

            operationCts?.Cancel();
            operationCts = new CancellationTokenSource();
            var token = operationCts.Token;

            setBusy(true);
            runOnUi(() => StatusMessage.Value = Loc.Get("StatusComputingSet"));

            try
            {
                var progress = createScanProgress();

                var scanResult = await dataService.CompareRealmSetsAsync(
                    SetOperation.Value,
                    SyncRealmIdA.Value,
                    SyncRealmIdB.Value,
                    EntityFilter.Value,
                    progress,
                    token).ConfigureAwait(false);

                runOnUi(() =>
                {
                    lastScanResult = scanResult;
                    StatusMessage.Value = Loc.Format(
                        "StatusSetComplete",
                        lastScanResult.SourceOnly.Count,
                        lastScanResult.TargetOnly.Count,
                        lastScanResult.Conflicted.Count);

                    Progress.Value = 1;
                    CurrentCategory.Value = DiffCategory.SourceOnly;
                    refreshSyncRows();
                    updateCanApply();
                });
            }
            catch (Exception ex)
            {
                runOnUi(() => StatusMessage.Value = ex.Message);
            }
            finally
            {
                setBusy(false);
            }
        }

        public async Task ExecuteSyncActionAsync()
        {
            var selected = syncRows.Where(r => r.IsSelected).Select(r => r.Item).ToList();

            if (selected.Count == 0)
            {
                runOnUi(() => StatusMessage.Value = Loc.Get("ErrorNoSelection"));
                return;
            }

            bool delete = SyncAction.Value == RealmSyncAction.Delete;
            var sourceFile = getRealmFile(SyncRealmIdA.Value);
            var targetFile = getRealmFile(SyncRealmIdB.Value);

            if (sourceFile == null || targetFile == null)
                return;

            if (ConfirmAsync == null)
                return;

            string confirmMessage = delete
                ? Loc.Format("ConfirmDelete", selected.Count, sourceFile.DisplayName)
                : Loc.Format("ConfirmAdd", selected.Count, targetFile.DisplayName);

            if (!await ConfirmAsync(confirmMessage, Loc.Get("ConfirmTitle"), delete).ConfigureAwait(false))
                return;

            operationCts?.Cancel();
            operationCts = new CancellationTokenSource();
            var token = operationCts.Token;

            setBusy(true);

            try
            {
                if (!RealmWritePlan.TryFromEndpoints(sourceFile, targetFile, out var writePlan, out string? planError) || writePlan == null)
                {
                    runOnUi(() => StatusMessage.Value = planError ?? Loc.Get("ErrorPickSourceTarget"));
                    return;
                }

                var pathConfig = writePlan.ToLegacyPathConfiguration();
                var validation = await syncService.ValidatePathsAsync(pathConfig, token).ConfigureAwait(false);

                if (!validation.IsValid)
                {
                    runOnUi(() => StatusMessage.Value = string.Join(Environment.NewLine, validation.Errors));
                    return;
                }

                if (validation.Warnings.Count > 0)
                    runOnUi(() => StatusMessage.Value = string.Join(Environment.NewLine, validation.Warnings));

                if (!delete)
                {
                    string backupPath = await dataService.CreateTimestampedBackupAsync(targetFile.FilePath, BackupDirectory.Value, token).ConfigureAwait(false);
                    runOnUi(() => StatusMessage.Value = Loc.Format("StatusBackupCreated", backupPath));
                }

                var request = new ApplyRequest
                {
                    WritePlan = writePlan,
                    Direction = writePlan.LegacyDirection,
                    Paths = writePlan.ToLegacyPathConfiguration(),
                    ItemIds = selected.Select(i => i.Id).ToList(),
                    CreateBackup = false,
                    DeleteFromSource = delete,
                };

                var progress = createApplyProgress();

                var result = await syncService.ApplyAsync(request, progress, token).ConfigureAwait(false);

                runOnUi(() =>
                {
                    StatusMessage.Value = delete
                        ? Loc.Format("StatusDeleted", result.AppliedCount, sourceFile.DisplayName)
                        : Loc.Format("StatusAdded", result.AppliedCount, string.Empty);

                    Progress.Value = 1;
                });

                await ComputeSetAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                runOnUi(() => StatusMessage.Value = ex.Message);
            }
            finally
            {
                setBusy(false);
            }
        }

        public void ToggleSyncSelectAll()
        {
            if (syncRows.Count == 0)
                return;

            bool anyUnselected = syncRows.Any(r => !r.IsSelected);
            foreach (var row in syncRows)
                row.IsSelected = anyUnselected;

            IsSelectAllMode.Value = anyUnselected;
            updateSelectionCount();
            SyncRowsChanged?.Invoke();
        }

        public void UpdateSyncSelectionFromGrid() => updateSelectionCount();

        public void SetSyncRowsChecked(IEnumerable<DiffRowModel> rows, bool isChecked)
        {
            foreach (var row in rows)
                row.IsSelected = isChecked;

            updateSelectionCount();
            SyncRowsChanged?.Invoke();
        }

        public void InvertSyncRowChecks()
        {
            foreach (var row in syncRows)
                row.IsSelected = !row.IsSelected;

            IsSelectAllMode.Value = syncRows.Count > 0 && syncRows.All(r => r.IsSelected);
            updateSelectionCount();
            SyncRowsChanged?.Invoke();
        }

        public async Task<bool> TryConfirmDeleteAsync(int count)
        {
            if (count <= 0)
                return false;

            if (!ConfirmBeforeDelete.Value)
                return true;

            if (ConfirmAsync == null)
                return true;

            return await ConfirmAsync(
                Loc.Format("ConfirmDeleteRows", count),
                Loc.Get("DeleteTitle"),
                true).ConfigureAwait(false);
        }

        public void SetBrowseRowsChecked(IEnumerable<RealmBrowseRowModel> rows, bool isChecked)
        {
            foreach (var row in rows)
                row.IsSelected = isChecked;

            DataSelectionCount.Value = BrowseRows.Count(r => r.IsSelected);
            BrowseTableChanged?.Invoke();
        }

        public void InvertBrowseRowChecks()
        {
            foreach (var row in BrowseRows)
                row.IsSelected = !row.IsSelected;

            DataSelectionCount.Value = BrowseRows.Count(r => r.IsSelected);
            BrowseTableChanged?.Invoke();
        }

        public async Task DeleteBrowseRowsAsync(IReadOnlyList<RealmBrowseRowModel> rows)
        {
            if (rows.Count == 0)
                return;

            if (!isMutableBrowseClass(SelectedRealmClass.Value))
            {
                runOnUi(() => StatusMessage.Value = Loc.Get("ErrorBrowseClassNotMutable"));
                return;
            }

            if (!await TryConfirmDeleteAsync(rows.Count).ConfigureAwait(false))
                return;

            var file = getRealmFile(DataRealmId.Value);
            if (file == null)
                return;

            setBusy(true);

            try
            {
                int deleted = await dataService.DeleteBrowseEntitiesAsync(
                    file.Id,
                    SelectedRealmClass.Value,
                    rows.Select(r => r.Id).ToList()).ConfigureAwait(false);

                if (deleted > 0)
                {
                    exportService.InvalidateCatalog(file.Id);
                    var progress = createScanProgress();
                    var snapshot = await dataService.LoadRealmSnapshotAsync(file.Id, progress).ConfigureAwait(false);

                    runOnUi(() =>
                    {
                        loadedSnapshot = snapshot;
                        refreshDataBrowse();
                        updateLoadedSnapshotSummary();
                        StatusMessage.Value = Loc.Format("StatusBrowseEntitiesDeleted", deleted);
                    });
                }
                else
                {
                    runOnUi(() => StatusMessage.Value = Loc.Get("ErrorBrowseDeleteNone"));
                }
            }
            catch (Exception ex)
            {
                runOnUi(() => StatusMessage.Value = ex.Message);
            }
            finally
            {
                setBusy(false);
            }
        }

        public async Task ExportBrowseRowsAsync(IReadOnlyList<RealmBrowseRowModel> rows)
        {
            if (rows.Count == 0)
                return;

            var objectClass = SelectedRealmClass.Value;

            if (!isExportableBrowseClass(objectClass))
            {
                runOnUi(() => StatusMessage.Value = Loc.Get("ErrorBrowseExportNotSupported"));
                return;
            }

            var file = getRealmFile(DataRealmId.Value);
            if (file == null)
                return;

            if (!RealmWorkspaceDiscovery.TryResolveSharedFilesDirectory(resolveWorkspaceRootForFile(file), out string filesDirectory))
            {
                runOnUi(() => StatusMessage.Value = Loc.Get("StatusFilesFolderRequired"));
                return;
            }

            setBusy(true);

            try
            {
                var progress = createScanProgress();
                var result = await exportService.ExportBrowseEntitiesAsync(
                    file.Id,
                    filesDirectory,
                    objectClass,
                    rows.Select(r => r.Id).ToList(),
                    ExportDirectory.Value,
                    groupScoresByPlayer: ExportGroupScoresByPlayer.Value,
                    progress: progress).ConfigureAwait(false);

                runOnUi(() => StatusMessage.Value = Loc.Format("StatusExportComplete", result.ExportedCount, result.OutputRoot));
            }
            catch (Exception ex)
            {
                runOnUi(() => StatusMessage.Value = ex.Message);
            }
            finally
            {
                setBusy(false);
            }
        }

        public static bool IsMutableBrowseClass(RealmObjectClass objectClass) => objectClass switch
        {
            RealmObjectClass.BeatmapSet => true,
            RealmObjectClass.Score => true,
            RealmObjectClass.BeatmapCollection => true,
            _ => false,
        };

        public static bool IsExportableBrowseClass(RealmObjectClass objectClass) => objectClass switch
        {
            RealmObjectClass.BeatmapSet => true,
            RealmObjectClass.Beatmap => true,
            RealmObjectClass.BeatmapCollection => true,
            RealmObjectClass.Score => true,
            _ => false,
        };

        private static bool isMutableBrowseClass(RealmObjectClass objectClass) => IsMutableBrowseClass(objectClass);

        private static bool isExportableBrowseClass(RealmObjectClass objectClass) => IsExportableBrowseClass(objectClass);

        public async Task DeleteSyncRowsAsync(IReadOnlyList<DiffRowModel> rows)
        {
            if (rows.Count == 0)
                return;

            if (!await TryConfirmDeleteAsync(rows.Count).ConfigureAwait(false))
                return;

            runOnUi(() =>
            {
                foreach (var row in rows)
                    syncRows.Remove(row);

                updateSelectionCount();
                SyncRowsChanged?.Invoke();
                StatusMessage.Value = Loc.Format("StatusBrowseRowsDeleted", rows.Count);
            });
        }

        public async Task DeleteFixIssuesAsync(IReadOnlyList<RealmFixIssueModel> issues)
        {
            if (issues.Count == 0)
                return;

            if (!await TryConfirmDeleteAsync(issues.Count).ConfigureAwait(false))
                return;

            runOnUi(() =>
            {
                foreach (var issue in issues)
                    FixIssues.Remove(issue);

                FixIssuesChanged?.Invoke();
                StatusMessage.Value = Loc.Format("StatusBrowseRowsDeleted", issues.Count);
            });
        }

        public void RemoveExportItemsFromList(IReadOnlyList<RealmExportItemModel> items)
        {
            if (items.Count == 0)
                return;

            runOnUi(() =>
            {
                foreach (var item in items)
                    ExportItems.Remove(item);

                ExportItemsChanged?.Invoke();
                StatusMessage.Value = Loc.Format("StatusExportListItemsRemoved", items.Count);
            });
        }

        public async Task ExportCheckedExportItemsAsync(IReadOnlyList<RealmExportItemModel> items)
        {
            if (items.Count == 0)
            {
                runOnUi(() => StatusMessage.Value = Loc.Get("ErrorNoSelection"));
                return;
            }

            foreach (var item in items)
                item.IsSelected = true;

            await ExportSelectedAsync().ConfigureAwait(false);
        }

        public async Task DeleteRealmFileRowsAsync(IReadOnlyList<RealmFileRowModel> rows)
        {
            if (rows.Count == 0)
                return;

            if (!await TryConfirmDeleteAsync(rows.Count).ConfigureAwait(false))
                return;

            runOnUi(() =>
            {
                foreach (var row in rows)
                {
                    var entry = RealmFiles.FirstOrDefault(f => f.Id == row.Id);
                    if (entry != null)
                        RealmFiles.Remove(entry);
                }

                refreshRealmFileRows();
                RealmFilesChanged?.Invoke();
                updateWorkspaceCapabilities();
                StatusMessage.Value = Loc.Format("StatusBrowseRowsDeleted", rows.Count);
            });
        }

        public void SetRealmFileRowsChecked(IEnumerable<RealmFileRowModel> rows, bool isChecked)
        {
            foreach (var row in rows)
                row.IsSelected = isChecked;
        }

        public void InvertRealmFileRowChecks()
        {
            foreach (var row in RealmFileRows)
                row.IsSelected = !row.IsSelected;
        }

        public async Task ScanFixIssuesAsync()
        {
            var file = getRealmFile(FixRealmId.Value);
            if (file == null)
                return;

            setBusy(true);

            try
            {
                var progress = createScanProgress();

                string replacement = string.IsNullOrEmpty(IllegalCharacterReplacement.Value)
                    ? "_"
                    : IllegalCharacterReplacement.Value[..1];

                var issues = await fixService.ScanIssuesAsync(
                    file.Id,
                    resolveWorkspaceRootForFile(file),
                    new RealmFixScanOptions { IllegalCharacterReplacement = replacement },
                    progress).ConfigureAwait(false);

                runOnUi(() =>
                {
                    FixIssues.Clear();
                    foreach (var issue in issues)
                        FixIssues.Add(new RealmFixIssueModel(issue));

                    FixIssuesChanged?.Invoke();
                    StatusMessage.Value = Loc.Format("StatusFixScanComplete", FixIssues.Count);
                    Progress.Value = 1;
                });
            }
            catch (Exception ex)
            {
                runOnUi(() => StatusMessage.Value = ex.Message);
            }
            finally
            {
                setBusy(false);
            }
        }

        public async Task ApplySelectedFixesAsync()
        {
            var selected = FixIssues.Where(i => i.IsSelected).Select(i => i.Id).ToList();

            if (selected.Count == 0)
            {
                runOnUi(() => StatusMessage.Value = Loc.Get("ErrorNoSelection"));
                return;
            }

            await applyFixesAsync(selected).ConfigureAwait(false);
        }

        public async Task ApplyAllFixesAsync() => await applyFixesAsync(FixIssues.Select(i => i.Id).ToList()).ConfigureAwait(false);

        public async Task ConvertSelectedFixRealmToOfficialAsync()
        {
            var file = getRealmFile(FixRealmId.Value);
            if (file == null)
                return;

            if (ConfirmAsync != null)
            {
                string backupDir = EzRealmSyncDefaults.DefaultBackupDirectory;
                string message = Loc.Format("FixConvertOfficialConfirm", backupDir);
                if (!await ConfirmAsync(message, Loc.Get("FixConvertOfficialTitle"), true).ConfigureAwait(false))
                    return;
            }

            setBusy(true);

            try
            {
                var progress = createScanProgress();
                var result = await fixService.ConvertToOfficialRealmAsync(file.Id, progress: progress).ConfigureAwait(false);
                await RefreshRealmFilesAsync().ConfigureAwait(false);

                runOnUi(() =>
                {
                    StatusMessage.Value = Loc.Format(
                        "StatusFixConvertedOfficial",
                        Path.GetFileName(result.TargetRealmFilePath),
                        result.BackupPath ?? EzRealmSyncDefaults.DefaultBackupDirectory,
                        result.AppliedCount);
                    Progress.Value = 1;
                });
            }
            catch (Exception ex)
            {
                runOnUi(() => StatusMessage.Value = ex.Message);
            }
            finally
            {
                setBusy(false);
            }
        }

        public async Task LoadExportCatalogAsync()
        {
            var file = getRealmFile(ExportRealmId.Value);
            if (file == null)
                return;

            setBusy(true);

            try
            {
                var progress = createScanProgress();
                var catalog = await exportService.LoadCatalogAsync(file.Id, SelectedExportKind.Value, progress).ConfigureAwait(false);

                runOnUi(() =>
                {
                    ExportItems.Clear();
                    foreach (var item in catalog.Items)
                        ExportItems.Add(new RealmExportItemModel(item));

                    ExportItemsChanged?.Invoke();
                    StatusMessage.Value = Loc.Format("StatusExportCatalogLoaded", ExportItems.Count);
                    Progress.Value = 1;
                });
            }
            catch (Exception ex)
            {
                runOnUi(() => StatusMessage.Value = ex.Message);
            }
            finally
            {
                setBusy(false);
            }
        }

        public void SetFixIssuesChecked(IEnumerable<RealmFixIssueModel> issues, bool isChecked)
        {
            foreach (var issue in issues)
                issue.IsSelected = isChecked;

            FixIssuesChanged?.Invoke();
        }

        public void InvertFixIssueChecks()
        {
            foreach (var issue in FixIssues)
                issue.IsSelected = !issue.IsSelected;

            FixIssuesChanged?.Invoke();
        }

        public void SetExportItemsChecked(IEnumerable<RealmExportItemModel> items, bool isChecked)
        {
            foreach (var item in items)
                item.IsSelected = isChecked;

            ExportItemsChanged?.Invoke();
        }

        public void InvertExportItemChecks()
        {
            foreach (var item in ExportItems)
                item.IsSelected = !item.IsSelected;

            ExportItemsChanged?.Invoke();
        }

        public void ClearExportItems()
        {
            ExportItems.Clear();
            ExportItemsChanged?.Invoke();
            exportService.InvalidateCatalog(ExportRealmId.Value);
        }

        public void ToggleFixSelectAll()
        {
            if (FixIssues.Count == 0)
                return;

            bool anyUnselected = FixIssues.Any(i => !i.IsSelected);
            SetFixIssuesChecked(FixIssues, anyUnselected);
        }

        public void ToggleExportSelectAll()
        {
            if (ExportItems.Count == 0)
                return;

            bool anyUnselected = ExportItems.Any(i => !i.IsSelected);
            SetExportItemsChecked(ExportItems, anyUnselected);
        }

        public async Task ExportSelectedAsync()
        {
            var file = getRealmFile(ExportRealmId.Value);
            if (file == null)
                return;

            var selected = ExportItems.Where(i => i.IsSelected).Select(i => i.Id).ToList();

            if (selected.Count == 0)
            {
                runOnUi(() => StatusMessage.Value = Loc.Get("ErrorNoSelection"));
                return;
            }

            if (!tryGetSharedFilesDirectory(out string filesDirectory) && !launchOptions.UiTestMode)
            {
                runOnUi(() => StatusMessage.Value = Loc.Get("StatusFilesFolderRequired"));
                return;
            }

            if (UiTestMode.Value && !tryGetSharedFilesDirectory(out filesDirectory))
                filesDirectory = Path.Combine(SearchDirectory.Value, "files");

            setBusy(true);

            try
            {
                var progress = createScanProgress();

                var result = await exportService.ExportAsync(
                    new RealmExportRequest
                    {
                        RealmId = file.Id,
                        Kind = SelectedExportKind.Value,
                        ItemIds = selected,
                        OutputDirectory = ExportDirectory.Value,
                        FolderName = string.IsNullOrWhiteSpace(ExportFolderName.Value) ? null : ExportFolderName.Value.Trim(),
                        FilesDirectory = filesDirectory,
                        GroupScoresByPlayer = ExportGroupScoresByPlayer.Value,
                    },
                    progress).ConfigureAwait(false);

                runOnUi(() =>
                {
                    StatusMessage.Value = Loc.Format("StatusExportComplete", result.ExportedCount, result.OutputRoot);
                    Progress.Value = 1;
                });
            }
            catch (Exception ex)
            {
                runOnUi(() => StatusMessage.Value = ex.Message);
            }
            finally
            {
                setBusy(false);
            }
        }

        public async Task BrowseExportDirectoryAsync()
        {
            if (PickFolderAsync == null)
                return;

            string? picked = await PickFolderAsync(ExportDirectory.Value).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(picked))
                runOnUi(() => ExportDirectory.Value = picked);
        }

        public void OnLanguageChanged() => LabelsChanged?.Invoke();

        private async Task applyFixesAsync(IReadOnlyList<Guid> issueIds)
        {
            var file = getRealmFile(FixRealmId.Value);
            if (file == null || issueIds.Count == 0)
                return;

            // 确认预览：按问题类型统计
            var selectedIssues = FixIssues.Where(i => issueIds.Contains(i.Id)).ToList();
            int missingCount = selectedIssues.Count(i => i.Issue.Kind == RealmFixIssueKind.MissingFile);
            int orphanCount = selectedIssues.Count(i => i.Issue.Kind == RealmFixIssueKind.OrphanFile);
            int illegalCount = selectedIssues.Count(i => i.Issue.Kind == RealmFixIssueKind.IllegalCharacter);

            string summary = Loc.Format("FixConfirmSummary",
                missingCount, orphanCount, illegalCount);

            if (ConfirmAsync != null && !await ConfirmAsync(summary, Loc.Get("FixConfirmTitle"), orphanCount > 0).ConfigureAwait(false))
                return;

            setBusy(true);

            try
            {
                string replacement = string.IsNullOrEmpty(IllegalCharacterReplacement.Value)
                    ? "_"
                    : IllegalCharacterReplacement.Value[..1];

                var progress = createScanProgress();

                var result = await fixService.ApplyFixesAsync(
                    file.Id,
                    resolveWorkspaceRootForFile(file),
                    issueIds,
                    new RealmFixApplyOptions { IllegalCharacterReplacement = replacement },
                    progress).ConfigureAwait(false);

                runOnUi(() =>
                {
                    var remaining = FixIssues.Where(i => !issueIds.Contains(i.Id)).ToList();
                    FixIssues.Clear();
                    foreach (var item in remaining)
                        FixIssues.Add(item);

                    FixIssuesChanged?.Invoke();
                    StatusMessage.Value = Loc.Format("StatusFixApplied", result.AppliedCount);
                });
            }
            catch (Exception ex)
            {
                runOnUi(() => StatusMessage.Value = ex.Message);
            }
            finally
            {
                setBusy(false);
            }
        }

        private void updateWorkspaceCapabilities()
        {
            bool hasRealm = RealmFiles.Count > 0;
            bool hasFiles = RealmWorkspaceDiscovery.TryResolveSharedFilesDirectory(SearchDirectory.Value, out _);
            CanUseFixAndExport.Value = launchOptions.UiTestMode || (hasRealm && hasFiles);
            WorkspaceCapabilitiesChanged?.Invoke();
        }

        private void refreshDataBrowse() => runOnUi(() =>
        {
            DataClasses.Clear();
            DataRows.Clear();

            if (loadedSnapshot == null)
            {
                browseRowsByClass.Clear();
                refreshBrowseTable();
                DataClassesChanged?.Invoke();
                DataRowsChanged?.Invoke();
                return;
            }

            browseRowsByClass.Clear();

            foreach (var group in loadedSnapshot.Classes)
            {
                browseRowsByClass[group.Class] = group.Rows.ToList();
                DataClasses.Add(new RealmClassListItemModel(group));
            }

            if (DataClasses.Count > 0
                && !DataClasses.Any(c => c.Class == SelectedRealmClass.Value))
            {
                SelectedRealmClass.Value = DataClasses[0].Class;
            }

            refreshBrowseTable();
            DataClassesChanged?.Invoke();
            DataRowsChanged?.Invoke();
        });

        private void refreshBrowseTable() => runOnUi(() =>
        {
            BrowseRows.Clear();

            if (loadedSnapshot == null)
            {
                BrowseColumns = Array.Empty<RealmColumnDefinition>();
                DataSelectionCount.Value = 0;
                BrowseTableChanged?.Invoke();
                return;
            }

            var group = loadedSnapshot.Classes.FirstOrDefault(c => c.Class == SelectedRealmClass.Value)
                        ?? loadedSnapshot.Classes.FirstOrDefault();

            if (group == null || !browseRowsByClass.TryGetValue(group.Class, out var rows))
            {
                BrowseColumns = Array.Empty<RealmColumnDefinition>();
                DataSelectionCount.Value = 0;
                BrowseTableChanged?.Invoke();
                return;
            }

            BrowseColumns = group.Columns;

            foreach (var row in rows)
                BrowseRows.Add(new RealmBrowseRowModel(row, group.Columns));

            DataSelectionCount.Value = BrowseRows.Count(r => r.IsSelected);
            BrowseTableChanged?.Invoke();
        });

        private void refreshRealmFileRows() => runOnUi(() =>
        {
            RealmFileRows.Clear();
            foreach (var file in RealmFiles)
                RealmFileRows.Add(new RealmFileRowModel(file));
        });

        private void applySettings(EzRealmSyncAppSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.SearchDirectory))
                SearchDirectory.Value = settings.SearchDirectory;

            ImportSelectedRealmId.Value = settings.ImportSelectedRealmId;
            DataRealmId.Value = settings.DataRealmId;
            SyncRealmIdA.Value = settings.SyncRealmIdA;
            SyncRealmIdB.Value = settings.SyncRealmIdB;
            FixRealmId.Value = settings.FixRealmId;
            ExportRealmId.Value = settings.ExportRealmId;

            BackupDirectory.Value = string.IsNullOrWhiteSpace(settings.BackupDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EzRealmSync", "backups")
                : settings.BackupDirectory;

            ExportDirectory.Value = string.IsNullOrWhiteSpace(settings.ExportDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EzRealmSync", "exports")
                : settings.ExportDirectory;

            if (!string.IsNullOrWhiteSpace(settings.ExportFolderName))
                ExportFolderName.Value = settings.ExportFolderName;

            ExportGroupScoresByPlayer.Value = settings.ExportGroupScoresByPlayer;

            IllegalCharacterReplacement.Value = string.IsNullOrWhiteSpace(settings.IllegalCharacterReplacement)
                ? "_"
                : settings.IllegalCharacterReplacement;

            ConfirmBeforeDelete.Value = settings.ConfirmBeforeDelete;
        }

        private void applyBackendMode(bool uiTest)
        {
            if (IsBusy.Value)
            {
                suppressBackendModeChange = true;
                UiTestMode.Value = !uiTest;
                suppressBackendModeChange = false;
                StatusMessage.Value = Loc.Get("StatusBusyCannotSwitchBackend");
                return;
            }

            services.SetUiTestMode(uiTest);
            BackendKind = services.BackendKind;
            clearSessionState();
            StatusMessage.Value = uiTest
                ? Loc.Get("StatusUiTest")
                : BackendKind switch
                {
                    EzRealmSyncBackendKind.Real => Loc.Get("StatusBackendSwitchedReal"),
                    EzRealmSyncBackendKind.Stub => Loc.Get("StatusMissingLib"),
                    _ => Loc.Get("StatusReady"),
                };

            LabelsChanged?.Invoke();
            updateWorkspaceCapabilities();

            _ = switchBackendAsync();
        }

        private async Task switchBackendAsync()
        {
            try
            {
                await RefreshRealmFilesAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                runOnUi(() => StatusMessage.Value = ex.Message);
            }
        }

        private void clearSessionState()
        {
            loadedSnapshot = null;
            lastScanResult = null;
            LoadedSnapshotSummary = string.Empty;
            syncRows.Clear();
            SyncRowsChanged?.Invoke();
            DataRows.Clear();
            DataClasses.Clear();
            BrowseRows.Clear();
            browseRowsByClass.Clear();
            FixIssues.Clear();
            ExportItems.Clear();
            BackupEntries.Clear();
            BackupEntriesChanged?.Invoke();
            refreshDataBrowse();
        }

        private string resolveBackendStatusMessage()
        {
            if (UiTestMode.Value)
                return Loc.Get("StatusUiTest");

            if (BackendKind == EzRealmSyncBackendKind.Real)
                return Loc.Get("StatusReady");

            if (EzRealmSyncBackend.IsOsuGameDllOnDisk && !EzRealmSyncBackend.IsRealBackendCompiled)
                return Loc.Get("StatusLibNeedsRebuild");

            return Loc.Get("StatusMissingLib");
        }

        private void persistSettings()
        {
            if (loadingSettings)
                return;

            AppSettingsStore.Save(new EzRealmSyncAppSettings
            {
                SearchDirectory = SearchDirectory.Value,
                ImportSelectedRealmId = ImportSelectedRealmId.Value,
                DataRealmId = DataRealmId.Value,
                SyncRealmIdA = SyncRealmIdA.Value,
                SyncRealmIdB = SyncRealmIdB.Value,
                FixRealmId = FixRealmId.Value,
                ExportRealmId = ExportRealmId.Value,
                BackupDirectory = BackupDirectory.Value,
                ExportDirectory = ExportDirectory.Value,
                ExportFolderName = ExportFolderName.Value,
                ExportGroupScoresByPlayer = ExportGroupScoresByPlayer.Value,
                IllegalCharacterReplacement = IllegalCharacterReplacement.Value,
                ConfirmBeforeDelete = ConfirmBeforeDelete.Value,
                UiTestMode = UiTestMode.Value,
            });
        }

        private void refreshDataClassCounts()
        {
            foreach (var item in DataClasses)
            {
                if (browseRowsByClass.TryGetValue(item.Class, out var rows))
                    item.Count = rows.Count;
            }

            DataClassesChanged?.Invoke();
        }

        private void updateLoadedSnapshotSummary()
        {
            if (loadedSnapshot == null)
                return;

            int total = browseRowsByClass.Values.Sum(r => r.Count);
            LoadedSnapshotSummary = Loc.Format("StatusRealmLoaded", loadedSnapshot.DisplayName, total);
        }

        private void refreshSyncRows() => runOnUi(() =>
        {
            syncRows.Clear();

            if (lastScanResult != null)
            {
                var items = CurrentCategory.Value switch
                {
                    DiffCategory.SourceOnly => lastScanResult.SourceOnly,
                    DiffCategory.TargetOnly => lastScanResult.TargetOnly,
                    DiffCategory.Conflicted => lastScanResult.Conflicted,
                    _ => Array.Empty<DiffItem>(),
                };

                foreach (var item in items)
                    syncRows.Add(new DiffRowModel(item));
            }

            IsSelectAllMode.Value = true;
            updateSelectionCount();
            SyncRowsChanged?.Invoke();
        });

        private void updateCanApply()
        {
            bool conflicted = CurrentCategory.Value == DiffCategory.Conflicted;
            CanApply.Value = !IsBusy.Value && !conflicted && SyncAction.Value == RealmSyncAction.Add;
        }

        private void updateSelectionCount() => SelectionCount.Value = syncRows.Count(r => r.IsSelected);

        private RealmFileEntry? getRealmFile(string? id) => string.IsNullOrEmpty(id) ? null : RealmFiles.FirstOrDefault(f => f.Id == id);

        private string resolveWorkspaceRootForFile(RealmFileEntry file)
        {
            if (RealmWorkspaceDiscovery.TryResolveStorageRoot(SearchDirectory.Value, out string storageRoot))
                return storageRoot;

            return RealmWorkspacePaths.ResolveStorageRoot(file.FilePath);
        }

        private bool tryGetSharedFilesDirectory(out string filesDirectory) => RealmWorkspaceDiscovery.TryResolveSharedFilesDirectory(SearchDirectory.Value, out filesDirectory);

        private void reconcileTabRealmSelections()
        {
            if (RealmFiles.Count == 0)
            {
                ImportSelectedRealmId.Value = null;
                DataRealmId.Value = null;
                SyncRealmIdA.Value = null;
                SyncRealmIdB.Value = null;
                FixRealmId.Value = null;
                ExportRealmId.Value = null;
                return;
            }

            string first = RealmFiles[0].Id;
            string second = RealmFiles.Count > 1 ? RealmFiles[1].Id : first;

            ImportSelectedRealmId.Value = pickRealmId(ImportSelectedRealmId.Value, first);
            DataRealmId.Value = pickRealmId(DataRealmId.Value, first);
            SyncRealmIdA.Value = pickRealmId(SyncRealmIdA.Value, first);
            SyncRealmIdB.Value = pickRealmId(SyncRealmIdB.Value, second);
            FixRealmId.Value = pickRealmId(FixRealmId.Value, first);
            ExportRealmId.Value = pickRealmId(ExportRealmId.Value, first);
        }

        private string pickRealmId(string? current, string fallback) => !string.IsNullOrEmpty(current) && RealmFiles.Any(f => f.Id == current) ? current : fallback;

        private void setBusy(bool busy) => runOnUi(() =>
        {
            IsBusy.Value = busy;
            updateCanApply();
        });

        private void runOnUi(Action action)
        {
            if (MarshalToUi != null)
                MarshalToUi(action);
            else
                action();
        }

        private IProgress<ScanProgress> createScanProgress() => new Progress<ScanProgress>(p => runOnUi(() =>
        {
            Progress.Value = p.Progress;
            StatusMessage.Value = p.Message;
        }));

        private IProgress<ApplyProgress> createApplyProgress() => new Progress<ApplyProgress>(p => runOnUi(() =>
        {
            Progress.Value = p.Progress;
            StatusMessage.Value = p.Message;
        }));

        public static string GetEntityFilterLabel(EntityKindFilter filter) => filter switch
        {
            EntityKindFilter.All => Loc.Get("EntityAll"),
            EntityKindFilter.BeatmapSet => Loc.Get("EntityBeatmapSet"),
            EntityKindFilter.Beatmap => Loc.Get("EntityBeatmap"),
            EntityKindFilter.Score => Loc.Get("EntityScore"),
            EntityKindFilter.BeatmapCollection => Loc.Get("EntityBeatmapCollection"),
            _ => filter.ToString(),
        };

        public static string GetSetOperationLabel(RealmSetOperation op) => op switch
        {
            RealmSetOperation.Intersection => Loc.Get("SetIntersection"),
            RealmSetOperation.Union => Loc.Get("SetUnion"),
            RealmSetOperation.Difference => Loc.Get("SetDifference"),
            RealmSetOperation.SymmetricDifference => Loc.Get("SetSymmetricDifference"),
            _ => op.ToString(),
        };

        public static string GetSyncActionLabel(RealmSyncAction action) => action switch
        {
            RealmSyncAction.Add => Loc.Get("ActionAdd"),
            RealmSyncAction.Delete => Loc.Get("ActionDelete"),
            _ => action.ToString(),
        };

        public static string GetEntityKindLabel(EntityKind kind) => kind switch
        {
            EntityKind.BeatmapSet => Loc.Get("EntityBeatmapSet"),
            EntityKind.Beatmap => Loc.Get("EntityBeatmap"),
            EntityKind.Score => Loc.Get("EntityScore"),
            EntityKind.BeatmapCollection => Loc.Get("EntityBeatmapCollection"),
            _ => kind.ToString(),
        };

        public static string GetFixIssueKindLabel(RealmFixIssueKind kind) => kind switch
        {
            RealmFixIssueKind.MissingFile => Loc.Get("FixIssueMissingFile"),
            RealmFixIssueKind.IllegalCharacter => Loc.Get("FixIssueIllegalCharacter"),
            RealmFixIssueKind.OrphanFile => Loc.Get("FixIssueOrphanFile"),
            _ => kind.ToString(),
        };

        public static string GetExportDataKindLabel(ExportDataKind kind) => kind switch
        {
            ExportDataKind.BeatmapSet => Loc.Get("ExportKindBeatmapSet"),
            ExportDataKind.Beatmap => Loc.Get("ExportKindBeatmap"),
            ExportDataKind.Collection => Loc.Get("ExportKindCollection"),
            ExportDataKind.Score => Loc.Get("ExportKindScore"),
            _ => kind.ToString(),
        };

        public static string GetRealmObjectClassLabel(RealmObjectClass @class) => @class switch
        {
            RealmObjectClass.Beatmap => Loc.Get("RealmClassBeatmap"),
            RealmObjectClass.BeatmapCollection => Loc.Get("RealmClassBeatmapCollection"),
            RealmObjectClass.BeatmapMetadata => Loc.Get("RealmClassBeatmapMetadata"),
            RealmObjectClass.BeatmapSet => Loc.Get("RealmClassBeatmapSet"),
            RealmObjectClass.File => Loc.Get("RealmClassFile"),
            RealmObjectClass.Ruleset => Loc.Get("RealmClassRuleset"),
            RealmObjectClass.Score => Loc.Get("RealmClassScore"),
            RealmObjectClass.Skin => Loc.Get("RealmClassSkin"),
            _ => @class.ToString(),
        };
    }
}
