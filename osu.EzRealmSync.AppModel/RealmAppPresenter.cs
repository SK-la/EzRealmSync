using System.Collections.ObjectModel;
using osu.EzRealmSync.AppModel.Localization;
using osu.Framework.Bindables;
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
        private readonly IEzRealmSyncService syncService;
        private readonly IRealmDataService dataService;
        private readonly IRealmFixService fixService;
        private readonly IRealmExportService exportService;
        private readonly EzRealmSyncLaunchOptions launchOptions;

        private ScanResult? lastScanResult;
        private CancellationTokenSource? operationCts;
        private readonly List<DiffRowModel> syncRows = new();
        private RealmSnapshot? loadedSnapshot;
        private readonly Dictionary<RealmObjectClass, List<RealmBrowseRow>> browseRowsByClass = new();
        private readonly bool loadingSettings;

        public RealmAppPresenter(
            IEzRealmSyncService syncService,
            IRealmDataService dataService,
            IRealmFixService fixService,
            IRealmExportService exportService,
            EzRealmSyncLaunchOptions launchOptions)
        {
            this.syncService = syncService;
            this.dataService = dataService;
            this.fixService = fixService;
            this.exportService = exportService;
            this.launchOptions = launchOptions;

            loadingSettings = true;
            applySettings(AppSettingsStore.Load());
            loadingSettings = false;
            UiTestMode.Value = launchOptions.UiTestMode;
            StatusMessage.Value = launchOptions.UiTestMode ? Loc.Get("StatusUiTest") : Loc.Get("StatusReady");

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
            ExportDirectory.BindValueChanged(_ => persistSettings());
            ExportFolderName.BindValueChanged(_ => persistSettings());
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
        public Bindable<string> SearchDirectory { get; } = new Bindable<string>(string.Empty);

        public BindableBool CanUseFixAndExport { get; } = new BindableBool();

        public Bindable<string?> FixRealmId { get; } = new Bindable<string?>();

        public Bindable<string?> ExportRealmId { get; } = new Bindable<string?>();

        public Bindable<ExportDataKind> SelectedExportKind { get; } = new Bindable<ExportDataKind>(ExportDataKind.BeatmapSet);

        public Bindable<string> IllegalCharacterReplacement { get; } = new Bindable<string>("_");

        public BindableBool ConfirmBeforeDelete { get; } = new BindableBool(true);

        public Bindable<string> ExportDirectory { get; } = new Bindable<string>(string.Empty);

        public Bindable<string> ExportFolderName { get; } = new Bindable<string>(string.Empty);

        public Bindable<string?> SelectedRealmId { get; } = new Bindable<string?>();
        public Bindable<string?> ActiveSourceRealmId { get; } = new Bindable<string?>();
        public Bindable<string?> ActiveTargetRealmId { get; } = new Bindable<string?>();

        public Bindable<EntityKind> SelectedDataGroup { get; } = new Bindable<EntityKind>(EntityKind.BeatmapSet);

        public Bindable<RealmObjectClass> SelectedRealmClass { get; } = new Bindable<RealmObjectClass>(RealmObjectClass.BeatmapSet);
        public Bindable<RealmSetOperation> SetOperation { get; } = new Bindable<RealmSetOperation>(RealmSetOperation.Difference);
        public Bindable<RealmSyncAction> SyncAction { get; } = new Bindable<RealmSyncAction>(RealmSyncAction.Add);

        public BindableBool UiTestMode { get; } = new BindableBool();
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
            if (string.IsNullOrWhiteSpace(SearchDirectory.Value)
                && !string.IsNullOrWhiteSpace(launchOptions.CreateDefaultPaths().EzDataPath))
            {
                SearchDirectory.Value = launchOptions.CreateDefaultPaths().EzDataPath;
                persistSettings();
            }

            await RefreshRealmFilesAsync().ConfigureAwait(false);

            runOnUi(() =>
            {
                if (RealmFiles.Count > 0)
                {
                    SelectedRealmId.Value = RealmFiles[0].Id;
                    FixRealmId.Value = RealmFiles[0].Id;
                    ExportRealmId.Value = RealmFiles[0].Id;
                    ActiveSourceRealmId.Value = RealmFiles[0].Id;
                    ActiveTargetRealmId.Value = RealmFiles.Count > 1 ? RealmFiles[1].Id : RealmFiles[0].Id;
                }

                updateWorkspaceCapabilities();
            });
        }

        public async Task RefreshRealmFilesAsync()
        {
            setBusy(true);

            try
            {
                var files = await dataService.DiscoverRealmFilesAsync(SearchDirectory.Value).ConfigureAwait(false);

                runOnUi(() =>
                {
                    RealmFiles.Clear();
                    foreach (var file in files)
                        RealmFiles.Add(file);

                    refreshRealmFileRows();
                    RealmFilesChanged?.Invoke();
                    updateWorkspaceCapabilities();

                    if (!string.IsNullOrWhiteSpace(SearchDirectory.Value)
                        && Directory.Exists(SearchDirectory.Value.Trim())
                        && RealmFiles.Count == 0
                        && RealmWorkspacePaths.FindRealmFiles(SearchDirectory.Value).Count == 0)
                    {
                        StatusMessage.Value = Loc.Get("StatusNoRealmInPath");
                    }
                    else
                    {
                        StatusMessage.Value = Loc.Format("StatusRealmList", RealmFiles.Count);
                    }
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

        public async Task ApplyRealmPathAsync()
        {
            string path = SearchDirectory.Value.Trim();
            if (string.IsNullOrEmpty(path))
                return;

            if (File.Exists(path) && path.EndsWith(".realm", StringComparison.OrdinalIgnoreCase))
            {
                await RegisterRealmFileAsync(path).ConfigureAwait(false);
                return;
            }

            if (Directory.Exists(path))
            {
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
                    SelectedRealmId.Value = entry.Id;
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

        public async Task BrowseRealmLocationAsync()
        {
            if (PickRealmPathAsync == null)
                return;

            string? picked = await PickRealmPathAsync(SearchDirectory.Value).ConfigureAwait(false);
            if (string.IsNullOrEmpty(picked))
                return;

            runOnUi(() => SearchDirectory.Value = picked);

            if (File.Exists(picked) && picked.EndsWith(".realm", StringComparison.OrdinalIgnoreCase))
                await RegisterRealmFileAsync(picked).ConfigureAwait(false);
            else
                await RefreshRealmFilesAsync().ConfigureAwait(false);
        }

        public async Task BackupSelectedRealmAsync()
        {
            var file = getRealmFile(SelectedRealmId.Value);
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
            var file = getRealmFile(SelectedRealmId.Value);
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
            if (string.IsNullOrEmpty(SelectedRealmId.Value))
                return;

            setBusy(true);

            try
            {
                var progress = createScanProgress();
                var snapshot = await dataService.LoadRealmSnapshotAsync(SelectedRealmId.Value, progress).ConfigureAwait(false);

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
            if (string.IsNullOrEmpty(ActiveSourceRealmId.Value) || string.IsNullOrEmpty(ActiveTargetRealmId.Value))
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
                    ActiveSourceRealmId.Value,
                    ActiveTargetRealmId.Value,
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
            var sourceFile = getRealmFile(ActiveSourceRealmId.Value);
            var targetFile = getRealmFile(ActiveTargetRealmId.Value);

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
                if (!RealmSyncDirectionHelper.TryInferDirection(sourceFile, targetFile, out var direction, out string? directionError))
                {
                    runOnUi(() => StatusMessage.Value = directionError ?? Loc.Get("ErrorPickSourceTarget"));
                    return;
                }

                if (!delete)
                {
                    string backupPath = await dataService.CreateTimestampedBackupAsync(targetFile.FilePath, BackupDirectory.Value, token).ConfigureAwait(false);
                    runOnUi(() => StatusMessage.Value = Loc.Format("StatusBackupCreated", backupPath));
                }

                var request = new ApplyRequest
                {
                    Direction = direction,
                    Paths = RealmSyncDirectionHelper.CreatePaths(sourceFile, targetFile, direction),
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

            if (!await TryConfirmDeleteAsync(rows.Count).ConfigureAwait(false))
                return;

            deleteBrowseRowsCore(rows.Select(r => r.Id).ToList());
        }

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

        public async Task DeleteExportItemsAsync(IReadOnlyList<RealmExportItemModel> items)
        {
            if (items.Count == 0)
                return;

            if (!await TryConfirmDeleteAsync(items.Count).ConfigureAwait(false))
                return;

            runOnUi(() =>
            {
                foreach (var item in items)
                    ExportItems.Remove(item);

                ExportItemsChanged?.Invoke();
                StatusMessage.Value = Loc.Format("StatusBrowseRowsDeleted", items.Count);
            });
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

        private void deleteBrowseRowsCore(IReadOnlyList<Guid> rowIds)
        {
            if (rowIds.Count == 0 || !browseRowsByClass.TryGetValue(SelectedRealmClass.Value, out var rows))
                return;

            int removed = rows.RemoveAll(r => rowIds.Contains(r.Id));

            if (removed == 0)
                return;

            refreshDataClassCounts();
            refreshBrowseTable();
            updateLoadedSnapshotSummary();
            StatusMessage.Value = Loc.Format("StatusBrowseRowsDeleted", removed);
        }

        public async Task ScanFixIssuesAsync()
        {
            var file = getRealmFile(FixRealmId.Value ?? SelectedRealmId.Value);
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
                    SearchDirectory.Value,
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

        public async Task LoadExportCatalogAsync()
        {
            var file = getRealmFile(ExportRealmId.Value ?? SelectedRealmId.Value);
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
            var file = getRealmFile(ExportRealmId.Value ?? SelectedRealmId.Value);
            if (file == null)
                return;

            var selected = ExportItems.Where(i => i.IsSelected).Select(i => i.Id).ToList();

            if (selected.Count == 0)
            {
                runOnUi(() => StatusMessage.Value = Loc.Get("ErrorNoSelection"));
                return;
            }

            if (!RealmWorkspacePaths.TryResolveFilesDirectory(SearchDirectory.Value, out string filesDirectory)
                && !launchOptions.UiTestMode)
            {
                runOnUi(() => StatusMessage.Value = Loc.Get("StatusFilesFolderRequired"));
                return;
            }

            if (launchOptions.UiTestMode && !RealmWorkspacePaths.TryResolveFilesDirectory(SearchDirectory.Value, out filesDirectory))
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
            var file = getRealmFile(FixRealmId.Value ?? SelectedRealmId.Value);
            if (file == null || issueIds.Count == 0)
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
                    SearchDirectory.Value,
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
            bool hasFiles = RealmWorkspacePaths.WorkspaceHasFilesFolder(SearchDirectory.Value);
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

            BackupDirectory.Value = string.IsNullOrWhiteSpace(settings.BackupDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EzRealmSync", "backups")
                : settings.BackupDirectory;

            ExportDirectory.Value = string.IsNullOrWhiteSpace(settings.ExportDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EzRealmSync", "exports")
                : settings.ExportDirectory;

            if (!string.IsNullOrWhiteSpace(settings.ExportFolderName))
                ExportFolderName.Value = settings.ExportFolderName;

            IllegalCharacterReplacement.Value = string.IsNullOrWhiteSpace(settings.IllegalCharacterReplacement)
                ? "_"
                : settings.IllegalCharacterReplacement;

            ConfirmBeforeDelete.Value = settings.ConfirmBeforeDelete;
        }

        private void persistSettings()
        {
            if (loadingSettings)
                return;

            AppSettingsStore.Save(new EzRealmSyncAppSettings
            {
                SearchDirectory = SearchDirectory.Value,
                BackupDirectory = BackupDirectory.Value,
                ExportDirectory = ExportDirectory.Value,
                ExportFolderName = ExportFolderName.Value,
                IllegalCharacterReplacement = IllegalCharacterReplacement.Value,
                ConfirmBeforeDelete = ConfirmBeforeDelete.Value,
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
            _ => kind.ToString(),
        };

        public static string GetFixIssueKindLabel(RealmFixIssueKind kind) => kind switch
        {
            RealmFixIssueKind.MissingFile => Loc.Get("FixIssueMissingFile"),
            RealmFixIssueKind.IllegalCharacter => Loc.Get("FixIssueIllegalCharacter"),
            _ => kind.ToString(),
        };

        public static string GetExportDataKindLabel(ExportDataKind kind) => kind switch
        {
            ExportDataKind.BeatmapSet => Loc.Get("ExportKindBeatmapSet"),
            ExportDataKind.Beatmap => Loc.Get("ExportKindBeatmap"),
            ExportDataKind.Collection => Loc.Get("ExportKindCollection"),
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
