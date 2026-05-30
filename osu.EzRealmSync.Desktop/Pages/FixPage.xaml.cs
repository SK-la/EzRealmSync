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
        private bool issuesGridBehaviorConfigured;

        public FixPage()
        {
            InitializeComponent();
            Loaded += (_, _) => bindIfReady();
            DataContextChanged += (_, _) => bindIfReady();
        }

        private void bindIfReady()
        {
            if (DataContext is not ShellViewModel shell)
                return;

            vm = shell;
            refreshLabels();
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

                if (e.PropertyName == nameof(ShellViewModel.CanUseFixAndExport))
                    Dispatcher.Invoke(updateEnabled);
            };

            vm.Presenter.FixIssuesChanged += () => Dispatcher.Invoke(() => IssuesGrid.ItemsSource = vm!.FixIssues);
            vm.Presenter.WorkspaceCapabilitiesChanged += () => Dispatcher.Invoke(updateEnabled);

            updateEnabled();
        }

        private void updateEnabled()
        {
            bool enabled = vm is { CanUseFixAndExport: true, IsBusy: false };
            ScanButton.IsEnabled = enabled;
            FixSelectedButton.IsEnabled = enabled;
            FixAllButton.IsEnabled = enabled;
            SelectAllButton.IsEnabled = enabled;
            RealmSelectCombo.IsEnabled = enabled;
            ReplacementBox.IsEnabled = enabled;
        }

        private void refreshLabels()
        {
            HintText.Text = Loc.Get("FixRequiresFilesHint");
            ReplacementLabel.Text = Loc.Get("FixReplacementChar");
            ScanButton.Content = Loc.Get("FixScan");
            FixSelectedButton.Content = Loc.Get("FixApplySelected");
            FixAllButton.Content = Loc.Get("FixApplyAll");
            SelectAllButton.Content = Loc.Get("SelectAll");
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
            if (IssuesGrid.Columns.Count > 0)
                return;

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
                Header = header,
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
    }
}
