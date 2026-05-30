using System.Windows.Data;

namespace osu.EzRealmSync.Desktop.Helpers
{
    /// <summary>
    /// 资源管理器风格复选框列：点勾选框只切换当前行勾选，不触发「清空其它勾选」。
    /// </summary>
    internal static class DataGridCheckColumnHelper
    {
        public const string IS_SELECTED_PROPERTY_NAME = "IsSelected";

        public static DataGridTemplateColumn CreateColumn(double width = 40)
        {
            var checkFactory = new FrameworkElementFactory(typeof(CheckBox));
            checkFactory.SetBinding(
                CheckBox.IsCheckedProperty,
                new Binding(IS_SELECTED_PROPERTY_NAME)
                {
                    Mode = BindingMode.OneWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                });
            checkFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            checkFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            checkFactory.AddHandler(
                UIElement.PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler(onCheckBoxPreviewMouseLeftButtonDown),
                handledEventsToo: true);

            return new DataGridTemplateColumn
            {
                Header = string.Empty,
                Width = width,
                CellTemplate = new DataTemplate { VisualTree = checkFactory },
            };
        }

        private static void onCheckBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not CheckBox checkBox || checkBox.DataContext == null)
                return;

            var prop = checkBox.DataContext.GetType().GetProperty(IS_SELECTED_PROPERTY_NAME);
            if (prop == null || prop.PropertyType != typeof(bool))
                return;

            bool current = (bool)prop.GetValue(checkBox.DataContext)!;
            CheckableDataGridHelper.SuppressNextSelectionSync();
            prop.SetValue(checkBox.DataContext, !current);
            e.Handled = true;
        }
    }
}
