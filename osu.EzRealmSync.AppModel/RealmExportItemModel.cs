using System.ComponentModel;
using System.Runtime.CompilerServices;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.AppModel
{
    public sealed class RealmExportItemModel : INotifyPropertyChanged
    {
        private bool isSelected;

        public RealmExportItemModel(RealmExportItem item)
        {
            Item = item;
        }

        public RealmExportItem Item { get; }

        public Guid Id => Item.Id;

        public string Title => Item.Title;

        public string Artist => Item.Artist;

        public string RelativePath => string.IsNullOrWhiteSpace(Item.DestinationRelativePath) ? Item.RelativePath : Item.DestinationRelativePath;

        public string CollectionName => Item.CollectionName ?? string.Empty;

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
