using System.ComponentModel;
using System.Runtime.CompilerServices;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.AppModel
{
    public sealed class RealmFileRowModel : INotifyPropertyChanged
    {
        private bool isSelected;

        public RealmFileRowModel(RealmFileEntry entry) => Entry = entry;

        public RealmFileEntry Entry { get; }

        public string Id => Entry.Id;

        public string DisplayName => Entry.DisplayName;

        public string OfficialSchemaDisplay => Entry.OfficialSchemaDisplay;

        public string EzSchemaDisplay => Entry.EzSchemaDisplay;

        public string SizeDisplay => Entry.SizeDisplay;

        public string FilePath => Entry.FilePath;

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
