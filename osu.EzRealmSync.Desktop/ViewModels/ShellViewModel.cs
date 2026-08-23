using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using osu.EzRealmSync.AppModel;
using osu.EzRealmSync.AppModel.Localization;
using osu.EzRealmSync.Desktop.Commands;
using osu.EzRealmSync.Desktop.Services;
using osu.Framework.Bindables;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.Desktop.ViewModels
{
    public sealed class ShellViewModel : INotifyPropertyChanged
    {
        public ShellViewModel(RealmAppPresenter presenter, EzRealmSyncLaunchOptions options)
        {
            Presenter = presenter;
            LaunchOptions = options;

            presenter.PickFolderAsync = path =>
            {
                var owner = Application.Current.MainWindow ?? throw new InvalidOperationException();
                return WpfUiDialogService.PickFolderAsync(owner, path);
            };

            presenter.PickRealmPathAsync = path =>
            {
                var owner = Application.Current.MainWindow ?? throw new InvalidOperationException();
                return WpfUiDialogService.PickRealmPathAsync(owner, path);
            };

            presenter.PickCollectionDbAsync = path =>
            {
                var owner = Application.Current.MainWindow ?? throw new InvalidOperationException();
                return WpfUiDialogService.PickCollectionDbAsync(owner, path);
            };

            presenter.ConfirmAsync = (message, title, dangerous) =>
            {
                var owner = Application.Current.MainWindow ?? throw new InvalidOperationException();
                return WpfUiDialogService.ConfirmAsync(owner, message, title, dangerous);
            };

            presenter.MarshalToUi = action =>
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess())
                    dispatcher.Invoke(action);
                else
                    action();
            };

            void reportAsyncError(Exception ex) => presenter.MarshalToUi?.Invoke(() => presenter.StatusMessage.Value = ex.Message);

            SafeAsyncInvoker.DefaultExceptionHandler = reportAsyncError;

            bindPresenter(presenter.IsBusy, nameof(IsBusy));
            bindPresenter(presenter.Progress, nameof(Progress));
            bindPresenter(presenter.StatusMessage, nameof(StatusMessage));
            bindPresenter(presenter.SelectionCount, nameof(SelectionCountText));
            bindPresenter(presenter.CurrentWorkspaceTab, nameof(CurrentTab));
            bindPresenter(presenter.BackupDirectory, nameof(BackupDirectory));
            bindPresenter(presenter.SearchDirectory, nameof(SearchDirectory));
            bindPresenter(presenter.SelectedBackupId, nameof(SelectedBackupId));
            bindPresenter(presenter.ImportSelectedRealmId, nameof(ImportSelectedRealmId));
            bindPresenter(presenter.DataRealmId, nameof(DataRealmId));
            bindPresenter(presenter.SyncRealmIdA, nameof(SyncRealmIdA));
            bindPresenter(presenter.SyncRealmIdB, nameof(SyncRealmIdB));
            bindPresenter(presenter.FixConvertPrimaryButtonLabel, nameof(FixConvertPrimaryButtonLabel));
            bindPresenter(presenter.CanUseFixConvertPrimary, nameof(CanUseFixConvertPrimary));
            bindPresenter(presenter.FixRealmId, nameof(FixRealmId));
            bindPresenter(presenter.ExportRealmId, nameof(ExportRealmId));
            bindPresenter(presenter.EntityFilter, nameof(EntityFilter));
            bindPresenter(presenter.CurrentCategory, nameof(CurrentCategory));
            bindPresenter(presenter.SetOperation, nameof(SetOperation));
            bindPresenter(presenter.SyncAction, nameof(SyncAction));
            bindPresenter(presenter.SyncWriteTarget, nameof(SyncWriteTarget));
            bindPresenter(presenter.SelectedDataGroup, nameof(SelectedDataGroup));
            bindPresenter(presenter.SelectedRealmClass, nameof(SelectedRealmClass));
            bindPresenter(presenter.CanUseFixAndExport, nameof(CanUseFixAndExport));
            bindPresenter(presenter.SelectedExportKind, nameof(ExportDataKind));
            bindPresenter(presenter.IllegalCharacterReplacement, nameof(IllegalCharacterReplacement));
            bindPresenter(presenter.ExportDirectory, nameof(ExportDirectory));
            bindPresenter(presenter.ExportFolderName, nameof(ExportFolderName));
            bindPresenter(presenter.ExportGroupScoresByPlayer, nameof(ExportGroupScoresByPlayer));
            bindPresenter(presenter.ConfirmBeforeDelete, nameof(ConfirmBeforeDelete));

            presenter.RealmFilesChanged += () => Application.Current.Dispatcher.Invoke(onRealmFilesChanged);
            presenter.SyncRowsChanged += () => Application.Current.Dispatcher.Invoke(onSyncRowsChanged);
            presenter.DataRowsChanged += () => Application.Current.Dispatcher.Invoke(onDataRowsChanged);
            presenter.DataClassesChanged += () => Application.Current.Dispatcher.Invoke(onDataClassesChanged);
            presenter.BrowseTableChanged += () => Application.Current.Dispatcher.Invoke(onBrowseTableChanged);
            presenter.LabelsChanged += () => Application.Current.Dispatcher.Invoke(() => OnPropertyChanged(nameof(WindowTitle)));
            presenter.FixConvertLabelsChanged += () => Application.Current.Dispatcher.Invoke(onFixConvertLabelsChanged);
            Loc.LanguageChanged += () => Application.Current.Dispatcher.Invoke(() => OnPropertyChanged(nameof(WindowTitle)));

            RefreshRealmFilesCommand = createAsyncCommand(() => presenter.RefreshRealmFilesAsync(), () => !IsBusy);
            ApplySearchDirectoryCommand = createAsyncCommand(() => presenter.ApplySearchDirectoryAsync(), () => !IsBusy);
            BackupSelectedCommand = createAsyncCommand(() => presenter.BackupSelectedRealmAsync(), () => !IsBusy);
            RefreshBackupsCommand = createAsyncCommand(() => presenter.RefreshBackupsAsync(), () => !IsBusy);
            RestoreBackupCommand = createAsyncCommand(() => presenter.RestoreSelectedBackupAsync(), () => !IsBusy);
            LoadRealmCommand = createAsyncCommand(() => presenter.LoadSelectedRealmAsync(), () => !IsBusy);
            ComputeSetCommand = createAsyncCommand(() => presenter.ComputeSetAsync(), () => !IsBusy);
            ExecuteSyncCommand = createAsyncCommand(() => presenter.ExecuteSyncActionAsync(), () => !IsBusy);
            BrowseBackupCommand = createAsyncCommand(presenter.BrowseBackupDirectoryAsync);
            BrowseSearchDirectoryCommand = createAsyncCommand(presenter.BrowseSearchDirectoryAsync);
            ToggleSelectAllCommand = new RelayCommand(presenter.ToggleSyncSelectAll);
            ScanFixIssuesCommand = createAsyncCommand(() => presenter.ScanFixIssuesAsync(), () => !IsBusy && CanUseFixAndExport);
            ApplyFixSelectedCommand = createAsyncCommand(() => presenter.ApplySelectedFixesAsync(), () => !IsBusy && CanUseFixAndExport);
            ApplyAllFixesCommand = createAsyncCommand(() => presenter.ApplyAllFixesAsync(), () => !IsBusy && CanUseFixAndExport);
            ConvertFixRealmPreserveCommand = createAsyncCommand(
                () => presenter.ConvertSelectedFixRealmToOfficialPrimaryAsync(),
                () => !IsBusy && CanUseFixAndExport && CanUseFixConvertPrimary);
            ConvertFixRealmToLibCommand = createAsyncCommand(
                () => presenter.ConvertSelectedFixRealmToOfficialAsync(OfficialConvertTarget.UpgradeToLibUpstream),
                () => !IsBusy && CanUseFixAndExport);
            UpgradeFixRealmSchemaCommand = createAsyncCommand(() => presenter.UpgradeSelectedFixRealmSchemaAsync(), () => !IsBusy && CanUseFixAndExport);
            ToggleFixSelectAllCommand = new RelayCommand(presenter.ToggleFixSelectAll);
            LoadExportCatalogCommand = createAsyncCommand(() => presenter.LoadExportCatalogAsync(), () => !IsBusy);
            ExportSelectedCommand = createAsyncCommand(() => presenter.ExportSelectedAsync(), () => !IsBusy);
            ImportCollectionDbCommand = createAsyncCommand(() => presenter.ImportCollectionDbAsync(), () => !IsBusy);
            ToggleExportSelectAllCommand = new RelayCommand(presenter.ToggleExportSelectAll);
            BrowseExportDirectoryCommand = createAsyncCommand(presenter.BrowseExportDirectoryAsync);

            SafeAsyncInvoker.Run(() => presenter.InitializeAsync(), reportAsyncError);

            AsyncRelayCommand createAsyncCommand(Func<Task> work, Func<bool>? canExecute = null)
            {
                var command = new AsyncRelayCommand(work, canExecute);
                command.UnhandledException += (_, ex) => reportAsyncError(ex);
                return command;
            }
        }

        public RealmAppPresenter Presenter { get; }

        public EzRealmSyncLaunchOptions LaunchOptions { get; }

        public ObservableCollection<RealmFileEntry> RealmFiles => Presenter.RealmFiles;
        public ObservableCollection<RealmEntityRowModel> DataRows => Presenter.DataRows;
        public ObservableCollection<RealmClassListItemModel> DataClasses => Presenter.DataClasses;
        public ObservableCollection<DiffRowModel> SyncRows { get; } = new ObservableCollection<DiffRowModel>();

        public ObservableCollection<RealmFixIssueModel> FixIssues => Presenter.FixIssues;

        public ObservableCollection<RealmExportItemModel> ExportItems => Presenter.ExportItems;

        public string WindowTitle
        {
            get
            {
                if (LaunchOptions.UiTestMode)
                    return Loc.Get("AppTitleUiTest");

                return Loc.Get("AppTitle");
            }
        }

        public bool IsBusy => Presenter.IsBusy.Value;
        public double Progress => Presenter.Progress.Value;
        public string StatusMessage => Presenter.StatusMessage.Value;
        public string SelectionCountText => Loc.Format("SelectionCount", Presenter.SelectionCount.Value);
        public string LoadedSnapshotSummary => Presenter.LoadedSnapshotSummary;

        public ObservableCollection<RealmBrowseRowModel> BrowseRows => Presenter.BrowseRows;

        public ObservableCollection<RealmFileRowModel> RealmFileRows => Presenter.RealmFileRows;

        public IReadOnlyList<RealmColumnDefinition> BrowseColumns => Presenter.BrowseColumns;

        public bool ConfirmBeforeDelete
        {
            get => Presenter.ConfirmBeforeDelete.Value;
            set => Presenter.ConfirmBeforeDelete.Value = value;
        }

        public MainWorkspaceTab CurrentTab
        {
            get => Presenter.CurrentWorkspaceTab.Value;
            set => Presenter.CurrentWorkspaceTab.Value = value;
        }

        public string BackupDirectory
        {
            get => Presenter.BackupDirectory.Value;
            set => Presenter.BackupDirectory.Value = value;
        }

        public string SearchDirectory
        {
            get => Presenter.SearchDirectory.Value;
            set => Presenter.SearchDirectory.Value = value;
        }

        public string? ImportSelectedRealmId
        {
            get => Presenter.ImportSelectedRealmId.Value;
            set => Presenter.ImportSelectedRealmId.Value = value;
        }

        public string? DataRealmId
        {
            get => Presenter.DataRealmId.Value;
            set => Presenter.DataRealmId.Value = value;
        }

        public string? SelectedBackupId
        {
            get => Presenter.SelectedBackupId.Value;
            set => Presenter.SelectedBackupId.Value = value;
        }

        public string? SyncRealmIdA
        {
            get => Presenter.SyncRealmIdA.Value;
            set => Presenter.SyncRealmIdA.Value = value;
        }

        public string? SyncRealmIdB
        {
            get => Presenter.SyncRealmIdB.Value;
            set => Presenter.SyncRealmIdB.Value = value;
        }

        public EntityKindFilter EntityFilter
        {
            get => Presenter.EntityFilter.Value;
            set => Presenter.EntityFilter.Value = value;
        }

        public DiffCategory CurrentCategory
        {
            get => Presenter.CurrentCategory.Value;
            set => Presenter.CurrentCategory.Value = value;
        }

        public RealmSetOperation SetOperation
        {
            get => Presenter.SetOperation.Value;
            set => Presenter.SetOperation.Value = value;
        }

        public RealmSyncAction SyncAction
        {
            get => Presenter.SyncAction.Value;
            set => Presenter.SyncAction.Value = value;
        }

        public SyncWriteEndpoint SyncWriteTarget
        {
            get => Presenter.SyncWriteTarget.Value;
            set => Presenter.SyncWriteTarget.Value = value;
        }

        public EntityKind SelectedDataGroup
        {
            get => Presenter.SelectedDataGroup.Value;
            set => Presenter.SelectedDataGroup.Value = value;
        }

        public RealmObjectClass SelectedRealmClass
        {
            get => Presenter.SelectedRealmClass.Value;
            set => Presenter.SelectedRealmClass.Value = value;
        }

        public bool CanUseFixAndExport => Presenter.CanUseFixAndExport.Value;

        public bool CanUseFixConvertPrimary => Presenter.CanUseFixConvertPrimary.Value;

        public string FixConvertPrimaryButtonLabel => Presenter.FixConvertPrimaryButtonLabel.Value;

        public string? FixRealmId
        {
            get => Presenter.FixRealmId.Value;
            set => Presenter.FixRealmId.Value = value;
        }

        public string? ExportRealmId
        {
            get => Presenter.ExportRealmId.Value;
            set => Presenter.ExportRealmId.Value = value;
        }

        public ExportDataKind ExportDataKind
        {
            get => Presenter.SelectedExportKind.Value;
            set => Presenter.SelectedExportKind.Value = value;
        }

        public string IllegalCharacterReplacement
        {
            get => Presenter.IllegalCharacterReplacement.Value;
            set => Presenter.IllegalCharacterReplacement.Value = value;
        }

        public string ExportDirectory
        {
            get => Presenter.ExportDirectory.Value;
            set => Presenter.ExportDirectory.Value = value;
        }

        public string ExportFolderName
        {
            get => Presenter.ExportFolderName.Value;
            set => Presenter.ExportFolderName.Value = value;
        }

        public bool ExportGroupScoresByPlayer
        {
            get => Presenter.ExportGroupScoresByPlayer.Value;
            set => Presenter.ExportGroupScoresByPlayer.Value = value;
        }

        public ICommand RefreshRealmFilesCommand { get; }
        public ICommand ApplySearchDirectoryCommand { get; }
        public ICommand BackupSelectedCommand { get; }
        public ICommand RefreshBackupsCommand { get; }
        public ICommand RestoreBackupCommand { get; }
        public ICommand LoadRealmCommand { get; }
        public ICommand ComputeSetCommand { get; }
        public ICommand ExecuteSyncCommand { get; }
        public ICommand BrowseBackupCommand { get; }
        public ICommand BrowseSearchDirectoryCommand { get; }
        public ICommand ToggleSelectAllCommand { get; }
        public ICommand ScanFixIssuesCommand { get; }
        public ICommand ApplyFixSelectedCommand { get; }
        public ICommand ApplyAllFixesCommand { get; }
        public ICommand ConvertFixRealmPreserveCommand { get; }
        public ICommand ConvertFixRealmToLibCommand { get; }
        public ICommand UpgradeFixRealmSchemaCommand { get; }
        public ICommand ToggleFixSelectAllCommand { get; }
        public ICommand LoadExportCatalogCommand { get; }
        public ICommand ExportSelectedCommand { get; }
        public ICommand ImportCollectionDbCommand { get; }
        public ICommand ToggleExportSelectAllCommand { get; }
        public ICommand BrowseExportDirectoryCommand { get; }

        public IEnumerable<EntityKindFilter> EntityFilters { get; } = Enum.GetValues<EntityKindFilter>();
        public IEnumerable<RealmSetOperation> SetOperations { get; } = Enum.GetValues<RealmSetOperation>();
        public IEnumerable<RealmSyncAction> SyncActions { get; } = Enum.GetValues<RealmSyncAction>();
        public IEnumerable<EntityKind> DataGroups { get; } = new[] { EntityKind.BeatmapSet, EntityKind.Beatmap, EntityKind.Score };

        public string GetEntityFilterLabel(EntityKindFilter f) => RealmAppPresenter.GetEntityFilterLabel(f);
        public string GetSetOperationLabel(RealmSetOperation op) => RealmAppPresenter.GetSetOperationLabel(op);
        public string GetSyncActionLabel(RealmSyncAction a) => RealmAppPresenter.GetSyncActionLabel(a);
        public string GetSyncWriteEndpointLabel(SyncWriteEndpoint e) => RealmAppPresenter.GetSyncWriteEndpointLabel(e);
        public string GetEntityKindLabel(EntityKind k) => RealmAppPresenter.GetEntityKindLabel(k);

        public string GetExportDataKindLabel(ExportDataKind k) => RealmAppPresenter.GetExportDataKindLabel(k);

        public void OnSyncSelectionChanged() => Presenter.UpdateSyncSelectionFromGrid();

        public async Task RegisterDroppedRealmAsync(string path) => await Presenter.RegisterRealmFileAsync(path).ConfigureAwait(false);

        private void onFixConvertLabelsChanged()
        {
            OnPropertyChanged(nameof(FixConvertPrimaryButtonLabel));
            OnPropertyChanged(nameof(CanUseFixConvertPrimary));
        }

        private void onRealmFilesChanged()
        {
            OnPropertyChanged(nameof(RealmFiles));
            OnPropertyChanged(nameof(RealmFileRows));
            OnPropertyChanged(nameof(CanUseFixAndExport));
            OnPropertyChanged(nameof(CanUseFixConvertPrimary));
            OnPropertyChanged(nameof(FixConvertPrimaryButtonLabel));
            refreshRealmComboSources();
        }

        private void onSyncRowsChanged()
        {
            SyncRows.Clear();
            foreach (var row in Presenter.SyncRows)
                SyncRows.Add(row);

            OnPropertyChanged(nameof(SelectionCountText));
        }

        private void onDataRowsChanged()
        {
            OnPropertyChanged(nameof(DataRows));
            OnPropertyChanged(nameof(LoadedSnapshotSummary));
        }

        private void onDataClassesChanged()
        {
            OnPropertyChanged(nameof(DataClasses));
            OnPropertyChanged(nameof(LoadedSnapshotSummary));
        }

        private void onBrowseTableChanged()
        {
            OnPropertyChanged(nameof(BrowseRows));
            OnPropertyChanged(nameof(BrowseColumns));
        }

        private void refreshRealmComboSources() => OnPropertyChanged(nameof(RealmFiles));

        private void bindPresenter<T>(Bindable<T> bindable, string name) => bindable.BindValueChanged(_ => Application.Current.Dispatcher.Invoke(() => OnPropertyChanged(name)));

        private void bindPresenter(BindableBool bindable, string name) => bindable.BindValueChanged(_ => Application.Current.Dispatcher.Invoke(() => OnPropertyChanged(name)));

        private void bindPresenter(BindableInt bindable, string name) => bindable.BindValueChanged(_ => Application.Current.Dispatcher.Invoke(() => OnPropertyChanged(name)));

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
