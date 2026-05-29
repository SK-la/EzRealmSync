namespace osu.EzRealmSync.AppModel
{
    /// <summary>
    /// 防止异步命令重入的简单门闩（与 UI 线程解耦，可单测）。
    /// </summary>
    public sealed class AsyncCommandGate
    {
        private int isRunning;

        public bool IsRunning => Volatile.Read(ref isRunning) != 0;

        public bool TryEnter(Func<bool>? canExecute = null)
        {
            if (IsRunning)
                return false;

            if (canExecute?.Invoke() == false)
                return false;

            Interlocked.Exchange(ref isRunning, 1);
            return true;
        }

        public void Exit() => Interlocked.Exchange(ref isRunning, 0);
    }
}
