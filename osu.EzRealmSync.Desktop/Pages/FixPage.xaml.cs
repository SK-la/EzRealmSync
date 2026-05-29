using System.Windows.Controls.Primitives;
using System.Windows.Data;
using osu.EzRealmSync.AppModel;
using osu.EzRealmSync.AppModel.Localization;
using osu.EzRealmSync.Desktop.ViewModels;

namespace osu.EzRealmSync.Desktop.Pages
{
    public partial class FixPage : UserControl
    {
        private ShellViewModel? vm;

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

            RealmSelectCombo.ItemsSource = vm.RealmFiles;
            RealmSelectCombo.SelectedValue = vm.FixRealmId ?? vm.SelectedRealmId;
            ReplacementBox.Text = vm.IllegalCharacterReplacement;

            IssuesGrid.ItemsSource = vm.FixIssues;

            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ShellViewModel.RealmFiles))
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
            bool enabled = vm?.CanUseFixAndExport == true && vm.IsBusy == false;
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
            RealmSelectCombo.SelectedValue = vm.FixRealmId ?? vm.SelectedRealmId;
        }

        private void setupGrid()
        {
            if (IssuesGrid.Columns.Count > 0)
                return;

            var checkFactory = new FrameworkElementFactory(typeof(CheckBox));
            checkFactory.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(RealmFixIssueModel.IsSelected)) { Mode = BindingMode.TwoWay });
            checkFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            checkFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

            IssuesGrid.Columns.Add(new DataGridTemplateColumn
            {
                Header = string.Empty,
                Width = 40,
                CellTemplate = new DataTemplate { VisualTree = checkFactory },
            });
            IssuesGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColFixKind"), Binding = new Binding(nameof(RealmFixIssueModel.KindDisplay)), Width = 100 });
            IssuesGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColKind"), Binding = new Binding(nameof(RealmFixIssueModel.EntityKindDisplay)), Width = 88 });
            IssuesGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColFixField"), Binding = new Binding(nameof(RealmFixIssueModel.FieldName)), Width = 72 });
            IssuesGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColFixCurrent"), Binding = new Binding(nameof(RealmFixIssueModel.CurrentValue)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            IssuesGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColFixSuggested"), Binding = new Binding(nameof(RealmFixIssueModel.SuggestedValue)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            IssuesGrid.Columns.Add(new DataGridTextColumn { Header = Loc.Get("ColFixDetail"), Binding = new Binding(nameof(RealmFixIssueModel.Detail)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
        }

        private void RealmSelectCombo_OnChanged(object sender, SelectionChangedEventArgs e)
        {
            if (vm == null || RealmSelectCombo.SelectedValue is not string id)
                return;

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
