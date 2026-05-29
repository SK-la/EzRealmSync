using System.Globalization;
using System.Windows.Data;
using osu.EzRealmSync.AppModel;

namespace osu.EzRealmSync.Desktop.Converters
{
    public sealed class BrowseCellConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is RealmBrowseRowModel row && parameter is string key)
                return row.GetCell(key);

            return string.Empty;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
