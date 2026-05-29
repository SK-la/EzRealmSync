using System.ComponentModel;
using System.Runtime.CompilerServices;
using osu.Game.EzRealmSync.Models;

namespace osu.EzRealmSync.AppModel
{
    public sealed class RealmFixIssueModel : INotifyPropertyChanged
    {
        private bool isSelected;

        public RealmFixIssueModel(RealmFixIssue issue)
        {
            Issue = issue;
        }

        public RealmFixIssue Issue { get; }

        public Guid Id => Issue.Id;

        public string KindDisplay => RealmAppPresenter.GetFixIssueKindLabel(Issue.Kind);

        public string EntityKindDisplay => RealmAppPresenter.GetEntityKindLabel(Issue.EntityKind);

        public string FieldName => Issue.FieldName;

        public string CurrentValue => Issue.CurrentValue;

        public string SuggestedValue => Issue.SuggestedValue;

        public string Detail => Issue.Detail;

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
