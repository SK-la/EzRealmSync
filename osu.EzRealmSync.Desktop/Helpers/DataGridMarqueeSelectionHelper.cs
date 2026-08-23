using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace osu.EzRealmSync.Desktop.Helpers
{
    /// <summary>
    /// DataGrid 拖拽框选：拖出矩形后按行视觉相交批量勾选。
    /// Ctrl 拖为追加；无修饰为替换。拖距过小则交给默认单击。
    /// </summary>
    internal static class DataGridMarqueeSelectionHelper
    {
        private const double DRAG_THRESHOLD = 4;

        public static void Attach<T>(
            DataGrid grid,
            Func<IEnumerable<T>> getAllItems,
            Action<IReadOnlyList<T>, bool> setItemsChecked)
            where T : class
        {
            Point? origin = null;
            bool marqueeActive = false;
            bool appendMode = false;
            MarqueeAdorner? adorner = null;
            AdornerLayer? layer = null;
            HashSet<T>? baselineChecked = null;

            grid.PreviewMouseLeftButtonDown += (_, e) =>
            {
                if (e.OriginalSource is DependencyObject source
                    && (findAncestor<DataGridColumnHeader>(source) != null
                        || findAncestor<ButtonBase>(source) != null
                        || findAncestor<CheckBox>(source) != null
                        || findAncestor<ScrollBar>(source) != null))
                {
                    return;
                }

                if (findAncestor<DataGridRow>(e.OriginalSource as DependencyObject) == null
                    && findAncestor<DataGridCellsPanel>(e.OriginalSource as DependencyObject) == null
                    && findAncestor<ScrollContentPresenter>(e.OriginalSource as DependencyObject) == null)
                {
                    return;
                }

                origin = e.GetPosition(grid);
                marqueeActive = false;
                appendMode = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
                baselineChecked = null;
            };

            grid.PreviewMouseMove += (_, e) =>
            {
                if (origin == null || e.LeftButton != MouseButtonState.Pressed)
                    return;

                var pos = e.GetPosition(grid);
                if (!marqueeActive)
                {
                    if (Math.Abs(pos.X - origin.Value.X) < DRAG_THRESHOLD
                        && Math.Abs(pos.Y - origin.Value.Y) < DRAG_THRESHOLD)
                        return;

                    marqueeActive = true;
                    grid.CaptureMouse();
                    CheckableDataGridHelper.SuppressNextSelectionSync();

                    baselineChecked = appendMode
                        ? getAllItems().Where(getIsSelected).ToHashSet()
                        : new HashSet<T>();

                    layer = AdornerLayer.GetAdornerLayer(grid);
                    if (layer != null)
                    {
                        adorner = new MarqueeAdorner(grid);
                        layer.Add(adorner);
                    }
                }

                if (adorner != null && origin != null)
                    adorner.UpdateRect(origin.Value, pos);

                var start = origin!.Value;
                applyMarqueeSelection(grid, getAllItems, setItemsChecked, start, pos, appendMode, baselineChecked!);
                e.Handled = true;
            };

            grid.PreviewMouseLeftButtonUp += (_, e) =>
            {
                if (!marqueeActive)
                {
                    origin = null;
                    return;
                }

                finishMarquee(grid, ref adorner, ref layer);
                origin = null;
                marqueeActive = false;
                baselineChecked = null;
                e.Handled = true;
            };

            grid.LostMouseCapture += (_, _) =>
            {
                if (!marqueeActive)
                    return;

                finishMarquee(grid, ref adorner, ref layer);
                origin = null;
                marqueeActive = false;
                baselineChecked = null;
            };
        }

        private static void finishMarquee(DataGrid grid, ref MarqueeAdorner? adorner, ref AdornerLayer? layer)
        {
            if (adorner != null && layer != null)
                layer.Remove(adorner);

            adorner = null;
            layer = null;

            if (grid.IsMouseCaptured)
                grid.ReleaseMouseCapture();
        }

        private static void applyMarqueeSelection<T>(
            DataGrid grid,
            Func<IEnumerable<T>> getAllItems,
            Action<IReadOnlyList<T>, bool> setItemsChecked,
            Point origin,
            Point current,
            bool appendMode,
            HashSet<T> baselineChecked)
            where T : class
        {
            var rect = new Rect(origin, current);
            var hit = new List<T>();

            foreach (var item in getAllItems())
            {
                if (grid.ItemContainerGenerator.ContainerFromItem(item) is not DataGridRow row)
                    continue;

                var topLeft = row.TransformToAncestor(grid).Transform(new Point(0, 0));
                var rowRect = new Rect(topLeft, new Size(row.ActualWidth, row.ActualHeight));
                if (rect.IntersectsWith(rowRect))
                    hit.Add(item);
            }

            var hitSet = hit.ToHashSet();
            var all = getAllItems().ToList();

            if (appendMode)
            {
                var toCheck = hit.Where(i => !baselineChecked.Contains(i)).ToList();
                var toUncheck = all
                                .Where(i => !baselineChecked.Contains(i) && !hitSet.Contains(i) && getIsSelected(i))
                                .ToList();

                if (toCheck.Count > 0)
                    setItemsChecked(toCheck, true);

                if (toUncheck.Count > 0)
                    setItemsChecked(toUncheck, false);
            }
            else
            {
                var toCheck = hit.Where(i => !getIsSelected(i)).ToList();
                var toUncheck = all.Where(i => getIsSelected(i) && !hitSet.Contains(i)).ToList();

                if (toUncheck.Count > 0)
                    setItemsChecked(toUncheck, false);

                if (toCheck.Count > 0)
                    setItemsChecked(toCheck, true);
            }

            CheckableDataGridHelper.SuppressNextSelectionSync();
            grid.SelectedItems.Clear();
            foreach (var item in hit)
                grid.SelectedItems.Add(item);
        }

        private static bool getIsSelected<T>(T item) where T : class
        {
            var prop = item.GetType().GetProperty(DataGridCheckColumnHelper.IS_SELECTED_PROPERTY_NAME);
            return prop?.PropertyType == typeof(bool) && prop.GetValue(item) is true;
        }

        private static T? findAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match)
                    return match;

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private sealed class MarqueeAdorner : Adorner
        {
            private readonly Rectangle rect;

            public MarqueeAdorner(UIElement adorned)
                : base(adorned)
            {
                IsHitTestVisible = false;
                rect = new Rectangle
                {
                    Stroke = new SolidColorBrush(Color.FromArgb(200, 0, 120, 215)),
                    StrokeThickness = 1,
                    Fill = new SolidColorBrush(Color.FromArgb(40, 0, 120, 215)),
                };
                AddVisualChild(rect);
            }

            public void UpdateRect(Point a, Point b)
            {
                var r = new Rect(a, b);
                rect.Width = Math.Max(0, r.Width);
                rect.Height = Math.Max(0, r.Height);
                rect.Arrange(r);
                InvalidateVisual();
            }

            protected override int VisualChildrenCount => 1;

            protected override Visual GetVisualChild(int index) => rect;

            protected override Size ArrangeOverride(Size finalSize)
            {
                return finalSize;
            }
        }
    }
}
