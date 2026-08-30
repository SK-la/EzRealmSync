using System.Collections;
using System.Reflection;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using osu.EzRealmSync.AppModel;
using osu.EzRealmSync.AppModel.Localization;
using WpfBorder = System.Windows.Controls.Border;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfStackPanel = System.Windows.Controls.StackPanel;

namespace osu.EzRealmSync.Desktop.Helpers
{
    /// <summary>
    /// Excel 风格列头筛选：搜索框；去重 ≤10 时提供多选勾选项。
    /// </summary>
    internal static class DataGridColumnFilterHelper
    {
        public const int MaxDistinctCheckboxValues = 10;

        private static readonly DependencyProperty filter_host_property =
            DependencyProperty.RegisterAttached(
                "FilterHost",
                typeof(ColumnFilterHost),
                typeof(DataGridColumnFilterHelper),
                new PropertyMetadata(null));

        public static void Attach(DataGrid grid)
        {
            if (grid.GetValue(filter_host_property) is ColumnFilterHost)
                return;

            grid.SetValue(filter_host_property, new ColumnFilterHost(grid));
        }

        public static object CreateFilterHeader(DataGrid grid, string title, Func<object, string> getCellText)
        {
            Attach(grid);
            var host = (ColumnFilterHost)grid.GetValue(filter_host_property)!;
            return host.CreateHeader(title, getCellText);
        }

        public static object CreatePropertyFilterHeader(DataGrid grid, string title, string propertyPath)
        {
            return CreateFilterHeader(grid, title, item => readProperty(item, propertyPath));
        }

        public static object CreateBrowseFilterHeader(DataGrid grid, string title, string propertyKey)
        {
            return CreateFilterHeader(grid, title, item =>
                item is RealmBrowseRowModel row ? row.GetCell(propertyKey) : string.Empty);
        }

        public static void ResetFilters(DataGrid grid)
        {
            if (grid.GetValue(filter_host_property) is ColumnFilterHost host)
                host.ResetAllFilters();
        }

        /// <summary>
        /// 列被清空重建前调用，丢弃旧列筛选状态。
        /// </summary>
        public static void ClearColumnFilters(DataGrid grid)
        {
            if (grid.GetValue(filter_host_property) is ColumnFilterHost host)
                host.ClearColumnDefinitions();
        }

        private static string readProperty(object? item, string propertyPath)
        {
            if (item == null || string.IsNullOrEmpty(propertyPath))
                return string.Empty;

            try
            {
                object? current = item;

                foreach (string segment in propertyPath.Split('.'))
                {
                    if (current == null)
                        return string.Empty;

                    var prop = current.GetType().GetProperty(segment, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                    if (prop == null)
                        return string.Empty;

                    current = prop.GetValue(current);
                }

                return current?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private sealed class ColumnFilterHost
        {
            private readonly DataGrid grid;
            private readonly List<ColumnFilterState> filters = new();
            private object? lastItemsSource;

            public ColumnFilterHost(DataGrid grid)
            {
                this.grid = grid;
                grid.Loaded += (_, _) => watchItemsSource();
            }

            public FrameworkElement CreateHeader(string title, Func<object, string> getCellText)
            {
                var state = new ColumnFilterState(this, title, getCellText);
                filters.Add(state);
                return state.CreateHeaderElement();
            }

            public void ResetAllFilters()
            {
                foreach (var filter in filters)
                    filter.Clear(apply: false);

                applyFilters();
            }

            public void ClearColumnDefinitions()
            {
                filters.Clear();
                applyFilters();
            }

            public void NotifyItemsSourceMayHaveChanged()
            {
                object? current = grid.ItemsSource;
                if (ReferenceEquals(current, lastItemsSource))
                    return;

                lastItemsSource = current;
                ResetAllFilters();
            }

            public void applyFilters()
            {
                watchItemsSource();

                if (grid.ItemsSource == null)
                    return;

                var view = CollectionViewSource.GetDefaultView(grid.ItemsSource);
                if (view == null)
                    return;

                var active = filters.Where(f => f.IsActive).ToList();

                if (active.Count == 0)
                {
                    view.Filter = null;
                    view.Refresh();
                    return;
                }

                view.Filter = item => active.All(f => f.Matches(item));
                view.Refresh();
            }

            public IEnumerable EnumerateSourceItems()
            {
                if (grid.ItemsSource == null)
                    yield break;

                foreach (object? item in grid.ItemsSource)
                    yield return item;
            }

            private void watchItemsSource()
            {
                object? current = grid.ItemsSource;
                if (ReferenceEquals(current, lastItemsSource))
                    return;

                lastItemsSource = current;
                foreach (var filter in filters)
                    filter.Clear(apply: false);
            }
        }

        private sealed class ColumnFilterState
        {
            private readonly ColumnFilterHost host;
            private readonly string title;
            private readonly Func<object, string> getCellText;

            private string searchText = string.Empty;
            private HashSet<string>? checkedValues;
            private bool showCheckboxes;
            private List<string> distinctValues = new();
            private Button? filterButton;

            public ColumnFilterState(ColumnFilterHost host, string title, Func<object, string> getCellText)
            {
                this.host = host;
                this.title = title;
                this.getCellText = getCellText;
            }

            public bool IsActive =>
                !string.IsNullOrWhiteSpace(searchText)
                || checkedValues is { Count: > 0 };

            public FrameworkElement CreateHeaderElement()
            {
                var panel = new DockPanel { LastChildFill = true };

                filterButton = new Button
                {
                    Content = new SymbolIcon { Symbol = SymbolRegular.Filter24, FontSize = 12 },
                    Padding = new Thickness(4, 2, 4, 2),
                    Margin = new Thickness(4, 0, 0, 0),
                    Appearance = ControlAppearance.Transparent,
                    ToolTip = Loc.Get("FilterColumn"),
                };
                DockPanel.SetDock(filterButton, Dock.Right);
                filterButton.Click += (_, _) => openPopup(filterButton);

                panel.Children.Add(filterButton);
                panel.Children.Add(new TextBlock
                {
                    Text = title,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                });

                updateButtonAppearance();
                return panel;
            }

            public bool Matches(object item)
            {
                string cell = getCellText(item);

                if (checkedValues is { Count: > 0 } && !checkedValues.Contains(cell))
                    return false;

                if (!string.IsNullOrWhiteSpace(searchText)
                    && cell.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;

                return true;
            }

            public void Clear(bool apply)
            {
                searchText = string.Empty;
                checkedValues = null;
                updateButtonAppearance();
                if (apply)
                    host.applyFilters();
            }

            private void openPopup(FrameworkElement placement)
            {
                host.NotifyItemsSourceMayHaveChanged();
                collectDistinct();

                // 主题 ControlFill 多为半透明；过滤菜单必须用不透明底，避免透出表格发虚。
                Brush background = resolveOpaquePopupBackground();
                Brush borderBrush = resolveOpaqueBrush("ControlStrokeColorDefaultBrush", Color.FromRgb(80, 80, 80))
                                    ?? new SolidColorBrush(Color.FromRgb(80, 80, 80));

                var root = new WpfBorder
                {
                    Background = background,
                    BorderBrush = borderBrush,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10),
                    CornerRadius = new CornerRadius(6),
                    MinWidth = 220,
                    MaxWidth = 320,
                    Opacity = 1,
                };

                var stack = new WpfStackPanel();
                var uiSearch = new TextBox
                {
                    Text = searchText,
                    Margin = new Thickness(0, 0, 0, 8),
                    PlaceholderText = Loc.Get("FilterSearchPlaceholder"),
                };
                stack.Children.Add(uiSearch);

                WpfStackPanel? checksPanel = null;

                if (showCheckboxes)
                {
                    checksPanel = new WpfStackPanel { Margin = new Thickness(0, 0, 0, 8) };
                    var selected = checkedValues ?? new HashSet<string>(StringComparer.Ordinal);

                    foreach (string value in distinctValues)
                    {
                        checksPanel.Children.Add(new WpfCheckBox
                        {
                            Content = string.IsNullOrEmpty(value) ? Loc.Get("FilterBlankValue") : value,
                            Tag = value,
                            IsChecked = selected.Contains(value),
                            Margin = new Thickness(0, 2, 0, 2),
                        });
                    }

                    stack.Children.Add(new ScrollViewer
                    {
                        MaxHeight = 200,
                        Content = checksPanel,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    });
                }

                var buttons = new WpfStackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                };

                var applyBtn = new Button
                {
                    Content = Loc.Get("FilterApply"),
                    Appearance = ControlAppearance.Primary,
                    Margin = new Thickness(0, 0, 8, 0),
                    Padding = new Thickness(12, 4, 12, 4),
                };
                var clearBtn = new Button
                {
                    Content = Loc.Get("FilterClear"),
                    Appearance = ControlAppearance.Secondary,
                    Padding = new Thickness(12, 4, 12, 4),
                };

                var popup = new Popup
                {
                    PlacementTarget = placement,
                    Placement = PlacementMode.Bottom,
                    StaysOpen = false,
                    AllowsTransparency = true,
                    Child = root,
                };

                applyBtn.Click += (_, _) =>
                {
                    searchText = uiSearch.Text.Trim();

                    if (checksPanel != null)
                    {
                        var selected = new HashSet<string>(StringComparer.Ordinal);

                        foreach (object? child in checksPanel.Children)
                        {
                            if (child is WpfCheckBox { IsChecked: true, Tag: string value })
                                selected.Add(value);
                        }

                        checkedValues = selected.Count > 0 ? selected : null;
                    }
                    else
                    {
                        checkedValues = null;
                    }

                    updateButtonAppearance();
                    host.applyFilters();
                    popup.IsOpen = false;
                };

                clearBtn.Click += (_, _) =>
                {
                    Clear(apply: true);
                    popup.IsOpen = false;
                };

                buttons.Children.Add(applyBtn);
                buttons.Children.Add(clearBtn);
                stack.Children.Add(buttons);
                root.Child = stack;
                popup.IsOpen = true;
            }

            private static Brush resolveOpaquePopupBackground()
            {
                foreach (string key in new[]
                         {
                             "SolidBackgroundFillColorBaseBrush",
                             "ApplicationBackgroundBrush",
                             "CardBackgroundFillColorDefaultBrush",
                             "ControlFillColorDefaultBrush",
                         })
                {
                    var brush = resolveOpaqueBrush(key, null);
                    if (brush != null)
                        return brush;
                }

                return new SolidColorBrush(Color.FromRgb(32, 32, 32));
            }

            private static Brush? resolveOpaqueBrush(string resourceKey, Color? fallback)
            {
                try
                {
                    object? resource = Application.Current.TryFindResource(resourceKey);

                    if (resource is SolidColorBrush solid)
                    {
                        var c = solid.Color;
                        return new SolidColorBrush(Color.FromArgb(255, c.R, c.G, c.B));
                    }

                    if (resource is Color color)
                        return new SolidColorBrush(Color.FromArgb(255, color.R, color.G, color.B));
                }
                catch
                {
                    // fall through
                }

                return fallback.HasValue ? new SolidColorBrush(fallback.Value) : null;
            }

            private void collectDistinct()
            {
                distinctValues = new List<string>();
                showCheckboxes = true;
                var seen = new HashSet<string>(StringComparer.Ordinal);

                foreach (object? item in host.EnumerateSourceItems())
                {
                    string cell = getCellText(item);
                    if (!seen.Add(cell))
                        continue;

                    distinctValues.Add(cell);

                    if (seen.Count > MaxDistinctCheckboxValues)
                    {
                        showCheckboxes = false;
                        distinctValues.Clear();
                        break;
                    }
                }

                if (showCheckboxes)
                    distinctValues.Sort(StringComparer.OrdinalIgnoreCase);
            }

            private void updateButtonAppearance()
            {
                if (filterButton == null)
                    return;

                filterButton.Appearance = IsActive ? ControlAppearance.Primary : ControlAppearance.Transparent;
                filterButton.Opacity = IsActive ? 1 : 0.75;
            }
        }
    }
}
