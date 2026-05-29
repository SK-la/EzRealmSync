using osu.EzRealmSync.AppModel.Localization;
using osu.Framework.Bindables;
using osu.Game.EzRealmSync.Abstractions;
using osu.Game.EzRealmSync.Mock;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.AppModel
{
    public sealed class SyncPresenter
    {
        private readonly IEzRealmSyncService syncService;
        private readonly EzRealmSyncLaunchOptions launchOptions;

        private ScanResult? lastScanResult;
        private CancellationTokenSource? operationCts;
        private readonly List<DiffRowModel> rows = new();

        public SyncPresenter(IEzRealmSyncService syncService, EzRealmSyncLaunchOptions launchOptions)
        {
            this.syncService = syncService;
            this.launchOptions = launchOptions;

            UiTestMode.Value = launchOptions.UiTestMode;
            var defaults = launchOptions.CreateDefaultPaths();
            EndpointAPath.Value = defaults.EzDataPath;
            EndpointBPath.Value = defaults.OfficialDataPath;
            StatusMessage.Value = launchOptions.UiTestMode ? Loc.Get("StatusUiTest") : Loc.Get("StatusReady");

            Direction.BindValueChanged(_ => refreshLabels(), true);
            CurrentCategory.BindValueChanged(_ =>
            {
                refreshRows();
                updateCanApply();
            }, true);
            EntityFilter.BindValueChanged(_ => { }, true);
        }

        public Bindable<string> EndpointAPath { get; } = new Bindable<string>(string.Empty);
        public Bindable<string> EndpointBPath { get; } = new Bindable<string>(string.Empty);
        public BindableBool UiTestMode { get; } = new BindableBool();
        public Bindable<SyncDirection> Direction { get; } = new Bindable<SyncDirection>(SyncDirection.EzToOfficial);
        public Bindable<EntityKindFilter> EntityFilter { get; } = new Bindable<EntityKindFilter>(EntityKindFilter.All);
        public Bindable<DiffCategory> CurrentCategory { get; } = new Bindable<DiffCategory>(DiffCategory.SourceOnly);
        public Bindable<string> StatusMessage { get; } = new Bindable<string>(string.Empty);
        public Bindable<double> Progress { get; } = new Bindable<double>();
        public BindableInt SelectionCount { get; } = new BindableInt();
        public BindableBool IsBusy { get; } = new BindableBool();
        public BindableBool CanApply { get; } = new BindableBool(true);
        public BindableBool IsSelectAllMode { get; } = new BindableBool(true);

        public string DeleteButtonText { get; private set; } = string.Empty;
        public string ApplyButtonText { get; private set; } = string.Empty;
        public string TabSourceOnlyLabel { get; private set; } = string.Empty;
        public string TabTargetOnlyLabel { get; private set; } = string.Empty;
        public string TabConflictedLabel { get; private set; } = Loc.Get("TabConflicted");

        public IReadOnlyList<DiffRowModel> Rows => rows;

        public MockEzRealmSyncService? MockService => syncService as MockEzRealmSyncService;

        public Func<string, string, bool, Task<bool>>? ConfirmAsync { get; set; }

        public Func<string, Task<string?>>? PickFolderAsync { get; set; }

        public event Action? RowsChanged;
        public event Action? LabelsChanged;

        public void OnLanguageChanged() => refreshLabels();

        public async Task InitializeAsync()
        {
            refreshLabels();

            if (launchOptions.UiTestMode)
                await ScanAsync().ConfigureAwait(false);
        }

        public async Task BrowseEndpointAAsync()
        {
            await browsePathAsync(EndpointAPath, isEndpointA: true).ConfigureAwait(false);
        }

        public async Task BrowseEndpointBAsync()
        {
            await browsePathAsync(EndpointBPath, isEndpointA: false).ConfigureAwait(false);
        }

        private async Task browsePathAsync(Bindable<string> target, bool isEndpointA)
        {
            if (launchOptions.UiTestMode)
            {
                target.Value = isEndpointA ? @"C:\Fake\Ez2Lazer\data" : @"C:\Fake\osu\data";
                return;
            }

            if (PickFolderAsync == null)
                return;

            string? picked = await PickFolderAsync(target.Value);

            if (!string.IsNullOrEmpty(picked))
                target.Value = picked;
        }

        public async Task ScanAsync()
        {
            operationCts?.Cancel();
            operationCts = new CancellationTokenSource();
            var token = operationCts.Token;

            setBusy(true);
            StatusMessage.Value = Loc.Get("StatusScanning");

            try
            {
                if (!launchOptions.UiTestMode)
                {
                    var validation = await syncService.ValidatePathsAsync(createPaths(), token).ConfigureAwait(false);

                    if (!validation.IsValid)
                    {
                        StatusMessage.Value = string.Join("\n", validation.Errors);
                        return;
                    }
                }

                var request = new ScanRequest
                {
                    Direction = Direction.Value,
                    Paths = createPaths(),
                    EntityKinds = getEntityKindsFromFilter(),
                };

                var progress = new Progress<ScanProgress>(p =>
                {
                    Progress.Value = p.Progress;
                    StatusMessage.Value = p.Message;
                });

                lastScanResult = await syncService.ScanAsync(request, progress, token).ConfigureAwait(false);

                SyncEndpointLabels.Get(Direction.Value, out string source, out string target);
                StatusMessage.Value = Loc.Format(
                    "StatusScanComplete",
                    source, source, lastScanResult.SourceOnly.Count,
                    target, lastScanResult.TargetOnly.Count, lastScanResult.Conflicted.Count);
                Progress.Value = 1;
                refreshRows();
                updateCanApply();
            }
            catch (Exception ex)
            {
                StatusMessage.Value = ex.Message;
            }
            finally
            {
                setBusy(false);
            }
        }

        public async Task ConfirmApplyAsync(bool deleteFromSource)
        {
            var selected = rows.Where(r => r.IsSelected).Select(r => r.Item).ToList();

            if (selected.Count == 0)
            {
                StatusMessage.Value = Loc.Get("ErrorNoSelection");
                return;
            }

            if (!deleteFromSource && CurrentCategory.Value == DiffCategory.Conflicted)
            {
                StatusMessage.Value = Loc.Get("ErrorConflictedApply");
                return;
            }

            SyncEndpointLabels.Get(Direction.Value, out string source, out string target);

            if (ConfirmAsync == null)
                return;

            bool confirmed = deleteFromSource
                ? await ConfirmAsync(Loc.Format("ConfirmDelete", selected.Count, source), Loc.Get("DeleteTitle"), true).ConfigureAwait(false)
                : await ConfirmAsync(Loc.Format("ConfirmAdd", selected.Count, target), Loc.Get("ConfirmTitle"), false).ConfigureAwait(false);

            if (confirmed)
                await ApplyAsync(selected, deleteFromSource).ConfigureAwait(false);
        }

        private async Task ApplyAsync(List<DiffItem> selected, bool deleteFromSource)
        {
            operationCts?.Cancel();
            operationCts = new CancellationTokenSource();
            var token = operationCts.Token;

            setBusy(true);

            try
            {
                var request = new ApplyRequest
                {
                    Direction = Direction.Value,
                    Paths = createPaths(),
                    ItemIds = selected.Select(i => i.Id).ToList(),
                    CreateBackup = !deleteFromSource,
                    DeleteFromSource = deleteFromSource,
                };

                var progress = new Progress<ApplyProgress>(p =>
                {
                    Progress.Value = p.Progress;
                    StatusMessage.Value = p.Message;
                });

                var result = await syncService.ApplyAsync(request, progress, token).ConfigureAwait(false);

                SyncEndpointLabels.Get(Direction.Value, out string source, out string _);

                StatusMessage.Value = deleteFromSource
                    ? Loc.Format("StatusDeleted", result.AppliedCount, source)
                    : Loc.Format("StatusAdded", result.AppliedCount,
                        result.BackupPath != null ? Loc.Format("StatusBackupSuffix", result.BackupPath) : string.Empty);
                Progress.Value = 1;
                await ScanAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                StatusMessage.Value = ex.Message;
            }
            finally
            {
                setBusy(false);
            }
        }

        public void ToggleSelectAll()
        {
            if (rows.Count == 0)
                return;

            bool anyUnselected = rows.Any(r => !r.IsSelected);
            foreach (var row in rows)
                row.IsSelected = anyUnselected;

            IsSelectAllMode.Value = anyUnselected;
            updateSelectionCount();
            RowsChanged?.Invoke();
        }

        public void UpdateSelectionFromGrid()
        {
            updateSelectionCount();
            IsSelectAllMode.Value = rows.Any(r => !r.IsSelected);
        }

        private void refreshRows()
        {
            rows.Clear();

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
                    rows.Add(new DiffRowModel(item));
            }

            IsSelectAllMode.Value = true;
            updateSelectionCount();
            RowsChanged?.Invoke();
        }

        private void refreshLabels()
        {
            SyncEndpointLabels.Get(Direction.Value, out string source, out string target);
            DeleteButtonText = Loc.Format("DeleteFromSource", source);
            ApplyButtonText = Loc.Format("AddToTarget", target);
            TabSourceOnlyLabel = Loc.Format("TabSourceOnly", source, target);
            TabTargetOnlyLabel = Loc.Format("TabTargetOnly", source, target);
            TabConflictedLabel = Loc.Get("TabConflicted");
            LabelsChanged?.Invoke();
        }

        private void updateCanApply()
        {
            bool conflicted = CurrentCategory.Value == DiffCategory.Conflicted;
            CanApply.Value = !IsBusy.Value && !conflicted;
        }

        private void updateSelectionCount()
        {
            SelectionCount.Value = rows.Count(r => r.IsSelected);
        }

        private PathConfiguration createPaths() => new()
        {
            EzDataPath = EndpointAPath.Value,
            OfficialDataPath = EndpointBPath.Value,
        };

        private List<EntityKind> getEntityKindsFromFilter() => EntityFilter.Value switch
        {
            EntityKindFilter.All => new List<EntityKind> { EntityKind.BeatmapSet, EntityKind.Beatmap, EntityKind.Score },
            EntityKindFilter.BeatmapSet => new List<EntityKind> { EntityKind.BeatmapSet },
            EntityKindFilter.Beatmap => new List<EntityKind> { EntityKind.Beatmap },
            EntityKindFilter.Score => new List<EntityKind> { EntityKind.Score },
            _ => new List<EntityKind>(),
        };

        private void setBusy(bool busy)
        {
            IsBusy.Value = busy;
            updateCanApply();
        }

        public static string GetEntityFilterLabel(EntityKindFilter filter) => filter switch
        {
            EntityKindFilter.All => Loc.Get("EntityAll"),
            EntityKindFilter.BeatmapSet => Loc.Get("EntityBeatmapSet"),
            EntityKindFilter.Beatmap => Loc.Get("EntityBeatmap"),
            EntityKindFilter.Score => Loc.Get("EntityScore"),
            _ => filter.ToString(),
        };
    }
}
