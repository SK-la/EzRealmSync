using osu.EzRealmSync.AppModel.Localization;

namespace osu.EzRealmSync.Desktop.Helpers
{
    internal static class CheckableDataGridHelper
    {
        private static int suppressSelectionSyncDepth;

        public static void SuppressNextSelectionSync() => suppressSelectionSyncDepth++;

        public static void Configure<T>(
            DataGrid grid,
            Func<IEnumerable<T>> getAllItems,
            Action<IReadOnlyList<T>, bool> setItemsChecked,
            Action invertAllChecks,
            Func<IReadOnlyList<T>, Task> deleteSelectionAsync,
            Action? afterSelectionChanged = null)
            where T : class
        {
            grid.ClipboardCopyMode = DataGridClipboardCopyMode.None;
            grid.SelectionUnit = DataGridSelectionUnit.FullRow;
            grid.SelectionMode = DataGridSelectionMode.Extended;

            grid.SelectionChanged += (_, e) =>
            {
                SyncSelectionToChecks(grid, e, getAllItems, setItemsChecked);
                afterSelectionChanged?.Invoke();
            };

            DataGridContextMenuHelper.AttachExclusive(grid, menu =>
            {
                DataGridContextMenuHelper.AddItem(menu, "check", Loc.Get("CtxCheck"), (_, _) => setItemsChecked(GetContextTargets(grid, getAllItems), true));

                DataGridContextMenuHelper.AddItem(menu, "uncheck", Loc.Get("CtxUncheck"), (_, _) => setItemsChecked(GetContextTargets(grid, getAllItems), false));

                DataGridContextMenuHelper.AddItem(menu, "invert", Loc.Get("CtxInvertCheck"), (_, _) => invertAllChecks());

                DataGridContextMenuHelper.AddItem(menu, "delete", Loc.Get("CtxDelete"), async (_, _) =>
                {
                    var targets = GetContextTargets(grid, getAllItems);
                    if (targets.Count > 0)
                        await deleteSelectionAsync(targets);
                });
            });
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

            foreach (var item in grid.SelectedItems)
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
    }
}
