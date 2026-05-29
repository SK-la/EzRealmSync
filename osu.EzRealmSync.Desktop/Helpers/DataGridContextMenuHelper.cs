namespace osu.EzRealmSync.Desktop.Helpers
{
    internal static class DataGridContextMenuHelper
    {
        private const string tag_prefix = "EzCtx.";

        public static void Attach(DataGrid grid, Action<ContextMenu> appendItems)
        {
            grid.ContextMenuOpening += (_, _) =>
            {
                grid.ContextMenu ??= new ContextMenu();
                removeTaggedItems(grid.ContextMenu);

                if (grid.ContextMenu.Items.Count > 0)
                    grid.ContextMenu.Items.Add(new Separator { Tag = tag_prefix + "sep" });

                appendItems(grid.ContextMenu);
            };
        }

        public static System.Windows.Controls.MenuItem AddItem(ContextMenu menu, string key, string header, RoutedEventHandler click)
        {
            var item = new System.Windows.Controls.MenuItem
            {
                Header = header,
                Tag = tag_prefix + key,
            };
            item.Click += click;
            menu.Items.Add(item);
            return item;
        }

        private static void removeTaggedItems(ContextMenu menu)
        {
            for (int i = menu.Items.Count - 1; i >= 0; i--)
            {
                if (menu.Items[i] is FrameworkElement { Tag: string tag } && tag.StartsWith(tag_prefix, StringComparison.Ordinal))
                    menu.Items.RemoveAt(i);
            }
        }
    }
}
