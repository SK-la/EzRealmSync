using osu.EzRealmSync.AppModel.Localization;

namespace osu.EzRealmSync.Desktop.Helpers
{
    internal static class CheckableDataGridHelper
    {
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
                syncSelectionToChecks(grid, e, getAllItems, setItemsChecked);
                afterSelectionChanged?.Invoke();
            };

            DataGridContextMenuHelper.AttachExclusive(grid, menu =>
            {
                DataGridContextMenuHelper.AddItem(menu, "check", Loc.Get("CtxCheck"), (_, _) => setItemsChecked(GetContextTargets<T>(grid), true));

                DataGridContextMenuHelper.AddItem(menu, "uncheck", Loc.Get("CtxUncheck"), (_, _) => setItemsChecked(GetContextTargets<T>(grid), false));

                DataGridContextMenuHelper.AddItem(menu, "invert", Loc.Get("CtxInvertCheck"), (_, _) => invertAllChecks());

                DataGridContextMenuHelper.AddItem(menu, "delete", Loc.Get("CtxDelete"), async (_, _) =>
                {
                    var targets = GetContextTargets<T>(grid);
                    if (targets.Count > 0)
                        await deleteSelectionAsync(targets);
                });
            });
        }

        private static void syncSelectionToChecks<T>(
            DataGrid grid,
            SelectionChangedEventArgs e,
            Func<IEnumerable<T>> getAllItems,
            Action<IReadOnlyList<T>, bool> setItemsChecked)
            where T : class
        {
            bool rangeOrMultiModifier = Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
                                        || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

            if (!rangeOrMultiModifier && grid.SelectedItems.Count == 1 && grid.SelectedItem is T single)
            {
                setItemsChecked(getAllItems().ToList(), false);
                setItemsChecked(new[] { single }, true);
                return;
            }

            var added = e.AddedItems.OfType<T>().ToList();
            var removed = e.RemovedItems.OfType<T>().ToList();

            if (added.Count > 0)
                setItemsChecked(added, true);

            if (removed.Count > 0)
                setItemsChecked(removed, false);
        }

        public static List<T> GetContextTargets<T>(DataGrid grid) where T : class
        {
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
    }
}
