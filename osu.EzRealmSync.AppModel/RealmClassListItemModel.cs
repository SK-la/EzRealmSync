using System.ComponentModel;
using System.Runtime.CompilerServices;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.AppModel
{
    public sealed class RealmClassListItemModel : INotifyPropertyChanged
    {
        public RealmClassListItemModel(RealmClassGroup group)
            : this(group.Class, group.Count)
        {
            Group = group;
        }

        public RealmClassListItemModel(RealmObjectClass @class, int count)
        {
            Class = @class;
            DisplayName = RealmAppPresenter.GetRealmObjectClassLabel(@class);
            countField = count;
        }

        public RealmClassGroup? Group { get; }

        public RealmObjectClass Class { get; }

        public string DisplayName { get; }

        private int countField;

        public int Count
        {
            get => countField;
            set
            {
                if (countField == value)
                    return;

                countField = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CountDisplay));
            }
        }

        public string CountDisplay => Count.ToString("N0");

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
