using System.Windows.Input;

namespace JiraDesktop.Wpf.Commands;

/// <summary>
/// A lightweight <see cref="ICommand"/> implementation backed by delegate callbacks,
/// suitable for binding ViewModel actions to UI controls via the Command pattern.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Predicate<object?>? _canExecute;
    private readonly Action<object?> _execute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Raises <see cref="CanExecuteChanged"/> so bound controls re-evaluate whether the command is enabled.
    /// </summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
