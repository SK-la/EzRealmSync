using System.Runtime.CompilerServices;
using osu.EzRealmSync.AppModel.Localization;

namespace osu.EzRealmSync.Desktop.Helpers
{
    internal static class CheckableDataGridHelper
    {
        private static int suppressSelectionSyncDepth;
        private static readonly ConditionalWeakTable<DataGrid, object> keyboard_marquee_attached = new();

        public static void SuppressNextSelectionSync() => suppressSelectionSyncDepth++;

        public static void Configure<T>(
            DataGrid grid,
            Func<IEnumerable<T>> getAllItems,
            Action<IReadOnlyList<T>, bool> setItemsChecked,
            Action invertAllChecks,
            Func<IReadOnlyList<T>, Task> deleteSelectionAsync,
            Func<IReadOnlyList<T>, Task>? exportSelectionAsync = null,
            string? removeFromListLabel = null,
            Action? afterSelectionChanged = null)
            where T : class
        {
            grid.ClipboardCopyMode = DataGridClipboardCopyMode.None;
            grid.SelectionUnit = DataGridSelectionUnit.FullRow;
            grid.SelectionMode = DataGridSelectionMode.Extended;
            grid.Focusable = true;

            grid.SelectionChanged += (_, e) =>
            {
                SyncSelectionToChecks(grid, e, getAllItems, setItemsChecked);
                afterSelectionChanged?.Invoke();
            };

            AttachKeyboardAndMarquee(grid, getAllItems, setItemsChecked);

            DataGridContextMenuHelper.AttachExclusive(grid, menu =>
            {
                DataGridContextMenuHelper.AddItem(menu, "check", Loc.Get("CtxCheck"), (_, _) => setItemsChecked(GetContextTargets(grid, getAllItems), true));

                DataGridContextMenuHelper.AddItem(menu, "uncheck", Loc.Get("CtxUncheck"), (_, _) => setItemsChecked(GetContextTargets(grid, getAllItems), false));

                DataGridContextMenuHelper.AddItem(menu, "invert", Loc.Get("CtxInvertCheck"), (_, _) => invertAllChecks());

                if (exportSelectionAsync != null)
                {
                    DataGridContextMenuHelper.AddItem(menu, "export", Loc.Get("CtxExport"), async (_, _) =>
                    {
                        var targets = GetContextTargets(grid, getAllItems);
                        if (targets.Count > 0)
                            await exportSelectionAsync(targets);
                    });
                }

                DataGridContextMenuHelper.AddItem(
                    menu,
                    "delete",
                    removeFromListLabel ?? Loc.Get("CtxDelete"),
                    async (_, _) =>
                    {
                        var targets = GetContextTargets(grid, getAllItems);
                        if (targets.Count > 0)
                            await deleteSelectionAsync(targets);
                    });
            });
        }

        /// <summary>
        /// 为未走完整 Configure 的网格挂载 Ctrl+A / Esc / 框选（如 Data 页自定义右键菜单）。
        /// </summary>
        public static void AttachKeyboardAndMarquee<T>(
            DataGrid grid,
            Func<IEnumerable<T>> getAllItems,
            Action<IReadOnlyList<T>, bool> setItemsChecked)
            where T : class
        {
            if (keyboard_marquee_attached.TryGetValue(grid, out _))
                return;

            keyboard_marquee_attached.Add(grid, new object());

            grid.InputBindings.Add(new KeyBinding(
                new RelaySelectCommand(() => selectAll(grid, getAllItems, setItemsChecked)),
                Key.A,
                ModifierKeys.Control));

            grid.InputBindings.Add(new KeyBinding(
                new RelaySelectCommand(() => clearAll(grid, getAllItems, setItemsChecked)),
                Key.Escape,
                ModifierKeys.None));

            grid.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    selectAll(grid, getAllItems, setItemsChecked);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    clearAll(grid, getAllItems, setItemsChecked);
                    e.Handled = true;
                }
            };

            DataGridMarqueeSelectionHelper.Attach(grid, getAllItems, setItemsChecked);
        }

        private static void selectAll<T>(
            DataGrid grid,
            Func<IEnumerable<T>> getAllItems,
            Action<IReadOnlyList<T>, bool> setItemsChecked)
            where T : class
        {
            var all = getAllItems().ToList();
            if (all.Count == 0)
                return;

            setItemsChecked(all, true);
            SuppressNextSelectionSync();
            grid.SelectAll();
        }

        private static void clearAll<T>(
            DataGrid grid,
            Func<IEnumerable<T>> getAllItems,
            Action<IReadOnlyList<T>, bool> setItemsChecked)
            where T : class
        {
            var all = getAllItems().ToList();
            if (all.Count == 0)
                return;

            setItemsChecked(all, false);
            SuppressNextSelectionSync();
            grid.UnselectAll();
        }

        public static void SyncSelectionToChecks<T>(
            DataGrid grid,
            SelectionChangedEventArgs e,
            Func<IEnumerable<T>> getAllItems,
            Action<IReadOnlyList<T>, bool> setItemsChecked)
            where T : class
        {
            if (suppressSelectionSyncDepth > 0)
            {
                suppressSelectionSyncDepth--;
                return;
            }

            bool rangeOrMultiModifier = Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
                                        || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

            if (!rangeOrMultiModifier)
            {
                var selected = grid.SelectedItems.OfType<T>().ToList();

                if (selected.Count > 0)
                {
                    setItemsChecked(getAllItems().ToList(), false);
                    setItemsChecked(selected, true);
                    return;
                }
            }

            var added = e.AddedItems.OfType<T>().ToList();
            var removed = e.RemovedItems.OfType<T>().ToList();

            if (added.Count > 0)
                setItemsChecked(added, true);

            if (removed.Count > 0)
                setItemsChecked(removed, false);
        }

        public static List<T> GetContextTargets<T>(DataGrid grid, Func<IEnumerable<T>>? getAllItems = null) where T : class
        {
            if (getAllItems != null)
            {
                var checkedItems = getAllItems()
                                   .Where(item => item != null && getIsSelected(item))
                                   .ToList();

                if (checkedItems.Count > 0)
                    return checkedItems;
            }

            var result = new List<T>();

            foreach (object? item in grid.SelectedItems)
            {
                if (item is T row)
                    result.Add(row);
            }

            if (result.Count == 0 && grid.CurrentItem is T current)
                result.Add(current);

            return result;
        }

        private static bool getIsSelected<T>(T item) where T : class
        {
            var prop = item.GetType().GetProperty(DataGridCheckColumnHelper.IS_SELECTED_PROPERTY_NAME);
            return prop?.PropertyType == typeof(bool) && prop.GetValue(item) is true;
        }

        private sealed class RelaySelectCommand : ICommand
        {
            private readonly Action execute;

            public RelaySelectCommand(Action execute) => this.execute = execute;

            public bool CanExecute(object? parameter) => true;

            public void Execute(object? parameter) => execute();

#pragma warning disable CS0067
            public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067
        }
    }
}
