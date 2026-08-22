using osu.EzRealmSync.AppModel;

namespace osu.EzRealmSync.Desktop.Commands
{
    public sealed class RelayCommand : ICommand
    {
        private readonly Action execute;
        private readonly Func<bool>? canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => execute();
    }

    public sealed class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> execute;
        private readonly Func<bool>? canExecute;
        private readonly AsyncCommandGate gate = new AsyncCommandGate();

        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public event EventHandler<Exception>? UnhandledException;

        public bool CanExecute(object? parameter) => !gate.IsRunning && (canExecute?.Invoke() ?? true);

        public void Execute(object? parameter)
        {
            if (!gate.TryEnter(canExecute))
                return;

            invalidateCanExecute();
            SafeAsyncInvoker.Run(runAsync, handleException);
        }

        private async Task runAsync()
        {
            try
            {
                await execute().ConfigureAwait(true);
            }
            finally
            {
                gate.Exit();
                invalidateCanExecute();
            }
        }

        private void handleException(Exception ex)
        {
            gate.Exit();
            invalidateCanExecute();
            UnhandledException?.Invoke(this, ex);
        }

        private void invalidateCanExecute() => CommandManager.InvalidateRequerySuggested();
    }
}
