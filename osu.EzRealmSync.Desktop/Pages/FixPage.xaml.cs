using System.Windows.Data;
using osu.EzRealmSync.AppModel;
using osu.EzRealmSync.AppModel.Localization;
using osu.EzRealmSync.Desktop.Helpers;
using osu.EzRealmSync.Desktop.ViewModels;

namespace osu.EzRealmSync.Desktop.Pages
{
    public partial class FixPage
    {
        private ShellViewModel? vm;
        private bool pageBound;
        private bool issuesGridBehaviorConfigured;
        private bool issuesGridConfigured;

        public FixPage()
        {
            InitializeComponent();
            Loaded += (_, _) => bindIfReady();
            DataContextChanged += (_, _) => bindIfReady();
        }

        private void bindIfReady()
        {
            if (pageBound || DataContext is not ShellViewModel shell)
                return;

            pageBound = true;
            vm = shell;
            refreshLabels();
            refreshConvertButtons();
            setupGrid();
            configureIssuesGridBehavior();

            refreshRealmCombo();
            ReplacementBox.Text = vm.IllegalCharacterReplacement;

            IssuesGrid.ItemsSource = vm.FixIssues;

            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ShellViewModel.RealmFiles)
                    || e.PropertyName == nameof(ShellViewModel.FixRealmId))
                    Dispatcher.Invoke(refreshRealmCombo);

                if (e.PropertyName == nameof(ShellViewModel.FixIssues))
                    Dispatcher.Invoke(() => IssuesGrid.ItemsSource = vm!.FixIssues);

                if (e.PropertyName == nameof(ShellViewModel.FixConvertPrimaryButtonLabel)
                    || e.PropertyName == nameof(ShellViewModel.CanUseFixConvertPrimary))
                    Dispatcher.Invoke(refreshConvertButtons);

                if (e.PropertyName == nameof(ShellViewModel.CanUseFixAndExport)
                    || e.PropertyName == nameof(ShellViewModel.IsBusy))
                    Dispatcher.Invoke(updateEnabled);
            };

            vm.Presenter.FixIssuesChanged += () => Dispatcher.Invoke(() =>
            {
                DataGridColumnFilterHelper.ResetFilters(IssuesGrid);
                IssuesGrid.ItemsSource = vm!.FixIssues;
            });
            vm.Presenter.WorkspaceCapabilitiesChanged += () => Dispatcher.Invoke(updateEnabled);

            vm.Presenter.FixConvertLabelsChanged += () => Dispatcher.Invoke(refreshConvertButtons);
            vm.Presenter.LabelsChanged += () => Dispatcher.Invoke(refreshLabels);

            updateEnabled();
        }

        private void updateEnabled()
        {
            bool enabled = vm is { CanUseFixAndExport: true, IsBusy: false };
            ScanButton.IsEnabled = enabled;
            FixSelectedButton.IsEnabled = enabled;
            FixAllButton.IsEnabled = enabled;
            SelectAllButton.IsEnabled = enabled;
            UpgradeSchemaButton.IsEnabled = enabled;
            ConvertOfficialPreserveButton.IsEnabled = enabled && vm!.CanUseFixConvertPrimary;
            ConvertOfficialToLibButton.IsEnabled = enabled;
            RealmSelectCombo.IsEnabled = enabled;
            ReplacementBox.IsEnabled = enabled;
        }

        private void refreshConvertButtons()
        {
            if (vm == null)
                return;

            ConvertOfficialPreserveButton.Content = string.IsNullOrWhiteSpace(vm.FixConvertPrimaryButtonLabel)
                ? Loc.Get("FixConvertOfficialRead")
                : vm.FixConvertPrimaryButtonLabel;
            ConvertOfficialToLibButton.Content = Loc.Get("FixConvertOfficialToLib");
            updateEnabled();
        }

        private void refreshLabels()
        {
            HintText.Text = Loc.Get("FixRequiresFilesHint");
            ReplacementLabel.Text = Loc.Get("FixReplacementChar");
            ScanButton.Content = Loc.Get("FixScan");
            FixSelectedButton.Content = Loc.Get("FixApplySelected");
            FixAllButton.Content = Loc.Get("FixApplyAll");
            SelectAllButton.Content = Loc.Get("SelectAll");
            UpgradeSchemaButton.Content = Loc.Get("FixUpgradeSchema");
            refreshConvertButtons();
        }

        private void refreshRealmCombo()
        {
            if (vm == null)
                return;

            RealmSelectCombo.ItemsSource = vm.RealmFiles;
            RealmSelectCombo.SelectedValue = vm.FixRealmId;
        }

        private void setupGrid()
        {
            if (issuesGridConfigured)
                return;

            issuesGridConfigured = true;

            IssuesGrid.Columns.Add(DataGridCheckColumnHelper.CreateColumn());
            addTextColumn(Loc.Get("ColFixKind"), nameof(RealmFixIssueModel.KindDisplay), 100);
            addTextColumn(Loc.Get("ColKind"), nameof(RealmFixIssueModel.EntityKindDisplay), 88);
            addTextColumn(Loc.Get("ColFixField"), nameof(RealmFixIssueModel.FieldName), 72);
            addTextColumn(Loc.Get("ColFixCurrent"), nameof(RealmFixIssueModel.CurrentValue), new DataGridLength(1, DataGridLengthUnitType.Star));
            addTextColumn(Loc.Get("ColFixSuggested"), nameof(RealmFixIssueModel.SuggestedValue), new DataGridLength(1, DataGridLengthUnitType.Star));
            addTextColumn(Loc.Get("ColFixDetail"), nameof(RealmFixIssueModel.Detail), new DataGridLength(1.2, DataGridLengthUnitType.Star));
        }

        private void addTextColumn(string header, string path, double width) => addTextColumn(header, path, new DataGridLength(width));

        private void addTextColumn(string header, string path, DataGridLength width)
        {
            IssuesGrid.Columns.Add(new DataGridTextColumn
            {
                Header = DataGridColumnFilterHelper.CreatePropertyFilterHeader(IssuesGrid, header, path),
                Binding = new Binding(path) { Mode = BindingMode.OneWay },
                Width = width,
                IsReadOnly = true,
            });
        }

        private void configureIssuesGridBehavior()
        {
            if (issuesGridBehaviorConfigured || vm == null)
                return;

            issuesGridBehaviorConfigured = true;

            CheckableDataGridHelper.Configure(
                IssuesGrid,
                () => vm.FixIssues,
                (rows, check) => vm.Presenter.SetFixIssuesChecked(rows, check),
                () => vm.Presenter.InvertFixIssueChecks(),
                rows => vm.Presenter.DeleteFixIssuesAsync(rows));
        }

        private void RealmSelectCombo_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (vm != null && RealmSelectCombo.SelectedValue is string id)
                vm.FixRealmId = id;
        }

        private void Scan_OnClick(object sender, RoutedEventArgs e)
        {
            if (vm == null)
                return;

            vm.IllegalCharacterReplacement = ReplacementBox.Text;
            vm.ScanFixIssuesCommand.Execute(null);
        }

        private void FixSelected_OnClick(object sender, RoutedEventArgs e) => vm?.ApplyFixSelectedCommand.Execute(null);

        private void FixAll_OnClick(object sender, RoutedEventArgs e) => vm?.ApplyAllFixesCommand.Execute(null);

        private void SelectAll_OnClick(object sender, RoutedEventArgs e) => vm?.ToggleFixSelectAllCommand.Execute(null);

        private void ConvertOfficialPreserve_OnClick(object sender, RoutedEventArgs e) => vm?.ConvertFixRealmPreserveCommand.Execute(null);

        private void ConvertOfficialToLib_OnClick(object sender, RoutedEventArgs e) => vm?.ConvertFixRealmToLibCommand.Execute(null);

        private void UpgradeSchema_OnClick(object sender, RoutedEventArgs e) => vm?.UpgradeFixRealmSchemaCommand.Execute(null);
    }
}
