using System.ComponentModel;
using System.Runtime.CompilerServices;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.AppModel
{
    public sealed class RealmEntityRowModel : INotifyPropertyChanged
    {
        private bool isSelected;

        public RealmEntityRowModel(RealmEntityRow row)
        {
            Row = row;
        }

        public RealmEntityRow Row { get; }

        public EntityKind EntityKind => Row.EntityKind;

        public string Title => Row.Title;

        public string Artist => Row.Artist;

        public string Hash => Row.Hash;

        public string Ruleset => Row.Ruleset;

        public string Date => Row.Date?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty;

        public string Extra => Row.Extra ?? string.Empty;

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

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
