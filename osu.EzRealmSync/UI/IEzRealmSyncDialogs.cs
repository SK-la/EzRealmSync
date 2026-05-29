namespace osu.EzRealmSync.UI
{
    public interface IEzRealmSyncDialogs
    {
        void PushConfirm(string message, Action onConfirm, Action? onCancel = null);

        void PushDangerous(string message, Action onConfirm, Action? onCancel = null);
    }
}
