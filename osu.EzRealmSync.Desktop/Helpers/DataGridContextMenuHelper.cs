using System.Windows.Controls.Primitives;

namespace osu.EzRealmSync.Desktop.Helpers
{
    internal static class DataGridContextMenuHelper
    {
        private const string tag_prefix = "EzCtx.";

        /// <summary>
        /// 禁用剪贴板菜单，仅显示自定义项。
        /// 注意：不得在 ContextMenuOpening 中设置 e.Handled=true，否则会阻止菜单弹出。
        /// </summary>
        public static void AttachExclusive(DataGrid grid, Action<ContextMenu> buildMenu)
        {
            grid.ClipboardCopyMode = DataGridClipboardCopyMode.None;

            var menu = new ContextMenu();
            buildMenu(menu);
            grid.ContextMenu = menu;

            // WPF-UI DataGrid 有时不触发标准 ContextMenu 打开，用手动打开作兜底
            grid.PreviewMouseRightButtonUp += (_, e) =>
            {
                if (e.ChangedButton != MouseButton.Right)
                    return;

                if (menu.Items.Count == 0)
                    return;

                menu.PlacementTarget = grid;
                menu.Placement = PlacementMode.MousePoint;
                menu.IsOpen = true;
                e.Handled = true;
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
    }
}
