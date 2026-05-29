using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using osu.EzRealmSync.AppModel;
using osu.EzRealmSync.AppModel.Localization;
using osu.EzRealmSync.Desktop.Commands;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.Desktop.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged
    {
        private readonly SyncPresenter presenter;
        private readonly EzRealmSyncLaunchOptions options;

        public MainViewModel(SyncPresenter presenter, EzRealmSyncLaunchOptions options)
        {
            this.presenter = presenter;
            this.options = options;

            DiffRows = new ObservableCollection<DiffRowModel>();

            presenter.PickFolderAsync = path =>
            {
                var dialog = new OpenFolderDialog();

                if (!string.IsNullOrWhiteSpace(path))
                    dialog.InitialDirectory = path;

                return Task.FromResult(dialog.ShowDialog() == true ? dialog.FolderName : null);
            };

            presenter.ConfirmAsync = (message, title, dangerous) =>
            {
                var result = MessageBox.Show(
                    message,
                    title,
                    MessageBoxButton.YesNo,
                    dangerous ? MessageBoxImage.Warning : MessageBoxImage.Question);
                return Task.FromResult(result == MessageBoxResult.Yes);
            };

            presenter.RowsChanged += () => Application.Current.Dispatcher.Invoke(refreshRows);
            presenter.LabelsChanged += () => Application.Current.Dispatcher.Invoke(refreshLabels);

            bindPresenter(presenter.EndpointAPath, nameof(EndpointAPath));
            bindPresenter(presenter.EndpointBPath, nameof(EndpointBPath));
            bindPresenter(presenter.StatusMessage, nameof(StatusMessage));
            bindPresenter(presenter.Progress, nameof(Progress));
            presenter.SelectionCount.BindValueChanged(_ => Application.Current.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(SelectionCountText));
                OnPropertyChanged(nameof(SelectAllButtonText));
            }));
            presenter.IsSelectAllMode.BindValueChanged(_ => Application.Current.Dispatcher.Invoke(() =>
                OnPropertyChanged(nameof(SelectAllButtonText))));
            bindPresenter(presenter.IsBusy, nameof(IsBusy));
            bindPresenter(presenter.CanApply, nameof(CanApply));
            bindPresenter(presenter.CurrentCategory, nameof(CurrentCategory));

            ScanCommand = new AsyncRelayCommand(() => presenter.ScanAsync(), () => !IsBusy);
            BrowseACommand = new AsyncRelayCommand(presenter.BrowseEndpointAAsync);
            BrowseBCommand = new AsyncRelayCommand(presenter.BrowseEndpointBAsync);
            ApplyCommand = new AsyncRelayCommand(() => presenter.ConfirmApplyAsync(deleteFromSource: false), () => CanApply && !IsBusy);
            DeleteCommand = new AsyncRelayCommand(() => presenter.ConfirmApplyAsync(deleteFromSource: true), () => !IsBusy);
            SelectAllCommand = new RelayCommand(presenter.ToggleSelectAll);
            OpenSettingsCommand = new RelayCommand(openSettings);

            Loc.LanguageChanged += () => Application.Current.Dispatcher.Invoke(onLanguageChanged);

            refreshLabels();
            _ = presenter.InitializeAsync();
        }

        public ObservableCollection<DiffRowModel> DiffRows { get; }

        public string WindowTitle => options.UiTestMode ? Loc.Get("AppTitleUiTest") : Loc.Get("AppTitle");

        public string EndpointAPath
        {
            get => presenter.EndpointAPath.Value;
            set => presenter.EndpointAPath.Value = value;
        }

        public string EndpointBPath
        {
            get => presenter.EndpointBPath.Value;
            set => presenter.EndpointBPath.Value = value;
        }

        public string StatusMessage
        {
            get => presenter.StatusMessage.Value;
            set => presenter.StatusMessage.Value = value;
        }

        public double Progress
        {
            get => presenter.Progress.Value;
            set => presenter.Progress.Value = value;
        }

        public string SelectionCountText => Loc.Format("SelectionCount", presenter.SelectionCount.Value);

        public bool IsBusy
        {
            get => presenter.IsBusy.Value;
            set => presenter.IsBusy.Value = value;
        }

        public bool CanApply
        {
            get => presenter.CanApply.Value;
            set => presenter.CanApply.Value = value;
        }

        public SyncDirection Direction
        {
            get => presenter.Direction.Value;
            set => presenter.Direction.Value = value;
        }

        public EntityKindFilter EntityFilter
        {
            get => presenter.EntityFilter.Value;
            set
            {
                presenter.EntityFilter.Value = value;
                OnPropertyChanged();
            }
        }

        public DiffCategory CurrentCategory
        {
            get => presenter.CurrentCategory.Value;
            set => presenter.CurrentCategory.Value = value;
        }

        public string DeleteButtonText => presenter.DeleteButtonText;
        public string ApplyButtonText => presenter.ApplyButtonText;
        public string TabSourceOnlyLabel => presenter.TabSourceOnlyLabel;
        public string TabTargetOnlyLabel => presenter.TabTargetOnlyLabel;
        public string TabConflictedLabel => presenter.TabConflictedLabel;
        public string SelectAllButtonText => presenter.IsSelectAllMode.Value ? Loc.Get("SelectAll") : Loc.Get("DeselectAll");

        public string LocSettings => Loc.Get("Settings");
        public string LocEndpointA => Loc.Get("EndpointA");
        public string LocEndpointB => Loc.Get("EndpointB");
        public string LocBrowse => Loc.Get("Browse");
        public string LocScanDiff => Loc.Get("ScanDiff");
        public string LocSyncDirection => Loc.Get("SyncDirection");
        public string LocDirectionAToB => Loc.Get("DirectionAToB");
        public string LocDirectionBToA => Loc.Get("DirectionBToA");
        public string LocEntityFilter => Loc.Get("EntityFilter");
        public string LocCollectionsPhase2 => Loc.Get("CollectionsPhase2");
        public string LocExportOsr => Loc.Get("ExportOsrPhase2");

        public ICommand ScanCommand { get; }
        public ICommand BrowseACommand { get; }
        public ICommand BrowseBCommand { get; }
        public ICommand ApplyCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand OpenSettingsCommand { get; }

        public IEnumerable<EntityKindFilter> EntityFilters { get; } = Enum.GetValues<EntityKindFilter>();

        public string GetEntityFilterLabel(EntityKindFilter filter) => SyncPresenter.GetEntityFilterLabel(filter);

        public void OnDiffSelectionChanged() => presenter.UpdateSelectionFromGrid();

        public Task BrowseAAsync() => presenter.BrowseEndpointAAsync();

        public Task BrowseBAsync() => presenter.BrowseEndpointBAsync();

        public event PropertyChangedEventHandler? PropertyChanged;

        private void bindPresenter<T>(osu.Framework.Bindables.Bindable<T> bindable, string propertyName)
        {
            bindable.BindValueChanged(_ => Application.Current.Dispatcher.Invoke(() => OnPropertyChanged(propertyName)));
        }

        private void bindPresenter(osu.Framework.Bindables.BindableInt bindable, string propertyName)
        {
            bindable.BindValueChanged(_ => Application.Current.Dispatcher.Invoke(() => OnPropertyChanged(propertyName)));
        }

        private void bindPresenter(osu.Framework.Bindables.BindableBool bindable, string propertyName)
        {
            bindable.BindValueChanged(_ => Application.Current.Dispatcher.Invoke(() => OnPropertyChanged(propertyName)));
        }

        private void refreshRows()
        {
            DiffRows.Clear();

            foreach (var row in presenter.Rows)
                DiffRows.Add(row);

            OnPropertyChanged(nameof(SelectionCountText));
            OnPropertyChanged(nameof(SelectAllButtonText));
        }

        private void refreshLabels()
        {
            OnPropertyChanged(nameof(DeleteButtonText));
            OnPropertyChanged(nameof(ApplyButtonText));
            OnPropertyChanged(nameof(TabSourceOnlyLabel));
            OnPropertyChanged(nameof(TabTargetOnlyLabel));
            OnPropertyChanged(nameof(TabConflictedLabel));
            OnPropertyChanged(nameof(WindowTitle));
            refreshLocalizedProperties();
        }

        private void onLanguageChanged()
        {
            presenter.OnLanguageChanged();
            refreshLabels();
            refreshRows();
        }

        private void refreshLocalizedProperties()
        {
            OnPropertyChanged(nameof(LocSettings));
            OnPropertyChanged(nameof(LocEndpointA));
            OnPropertyChanged(nameof(LocEndpointB));
            OnPropertyChanged(nameof(LocBrowse));
            OnPropertyChanged(nameof(LocScanDiff));
            OnPropertyChanged(nameof(LocSyncDirection));
            OnPropertyChanged(nameof(LocDirectionAToB));
            OnPropertyChanged(nameof(LocDirectionBToA));
            OnPropertyChanged(nameof(LocEntityFilter));
            OnPropertyChanged(nameof(LocCollectionsPhase2));
            OnPropertyChanged(nameof(LocExportOsr));
            OnPropertyChanged(nameof(SelectAllButtonText));
        }

        private void openSettings()
        {
            var settings = new SettingsWindow(presenter, options);
            settings.Owner = Application.Current.MainWindow;
            settings.ShowDialog();
            OnPropertyChanged(nameof(WindowTitle));
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
