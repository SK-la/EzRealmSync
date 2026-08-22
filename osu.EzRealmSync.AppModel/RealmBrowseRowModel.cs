using System.ComponentModel;
using System.Runtime.CompilerServices;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.AppModel
{
    public sealed class RealmBrowseRowModel : INotifyPropertyChanged
    {
        private bool isSelected;

        public RealmBrowseRowModel(RealmBrowseRow row, IReadOnlyList<RealmColumnDefinition> columns)
        {
            Id = row.Id;
            foreach (var column in columns)
                values[column.PropertyKey] = row.Cells.TryGetValue(column.PropertyKey, out string? value) ? value : string.Empty;
        }

        private readonly Dictionary<string, string> values = new Dictionary<string, string>();

        public Guid Id { get; }

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (isSelected == value)
                    return;

                isSelected = value;
                OnPropertyChanged();
            }
        }

        public string GetCell(string propertyKey) => values.TryGetValue(propertyKey, out string? v) ? v : string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
