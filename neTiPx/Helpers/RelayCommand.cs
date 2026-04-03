using System;
using System.Windows.Input;

namespace neTiPx.Helpers
{
    public sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => _execute();

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public sealed class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            if (_canExecute == null)
            {
                return true;
            }

            if (!TryGetParameter(parameter, out var typedParameter))
            {
                return false;
            }

            return _canExecute(typedParameter);
        }

        public void Execute(object? parameter)
        {
            if (!TryGetParameter(parameter, out var typedParameter))
            {
                typedParameter = default;
            }

            _execute(typedParameter);
        }

        private static bool TryGetParameter(object? parameter, out T? typedParameter)
        {
            if (parameter is null)
            {
                typedParameter = default;
                return true;
            }

            if (parameter is T casted)
            {
                typedParameter = casted;
                return true;
            }

            typedParameter = default;
            return false;
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
