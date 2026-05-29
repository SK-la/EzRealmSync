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
        private bool isRunning;

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

        public bool CanExecute(object? parameter) => !isRunning && (canExecute?.Invoke() ?? true);

        public async void Execute(object? parameter)
        {
            if (isRunning)
                return;

            isRunning = true;
            CommandManager.InvalidateRequerySuggested();

            try
            {
                await execute().ConfigureAwait(true);
            }
            finally
            {
                isRunning = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
}
