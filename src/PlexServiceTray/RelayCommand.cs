using System;
using System.Windows.Input;

namespace PlexServiceTray
{
    public class RelayCommand : ICommand
    {
        #region Fields

        private readonly Action<object> _execute;
        private readonly Predicate<object>? _canExecute;

        #endregion // Fields

        #region Constructors

        public RelayCommand(Action<object> execute, Predicate<object>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        #endregion // Constructors

        #region ICommand Members

        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || parameter == null || _canExecute(parameter);
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public void Execute(object? parameter) {
            if (parameter != null) {
                _execute(parameter);
            }
        }

        #endregion // RelayCommand Members
    }
}
