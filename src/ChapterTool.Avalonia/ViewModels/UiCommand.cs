using System.Windows.Input;
using System.ComponentModel;

namespace ChapterTool.Avalonia.ViewModels;

public sealed class UiCommand(
    Func<object?, CancellationToken, ValueTask> execute,
    Func<object?, bool>? canExecute = null)
    : ICommand, INotifyPropertyChanged
{
    private readonly Func<object?, bool> canExecute = canExecute ?? (_ => true);

    public UiCommand(Func<object?, ValueTask> execute, Func<object?, bool>? canExecute = null)
        : this((parameter, _) => execute(parameter), canExecute)
    {
    }

    public event EventHandler? CanExecuteChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsExecuting
    {
        get;
        private set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExecuting)));
            RaiseCanExecuteChanged();
        }
    }

    public Exception? ExecutionError
    {
        get;
        private set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExecutionError)));
        }
    }

    public bool CanExecute(object? parameter = null) => !IsExecuting && canExecute(parameter);

    public async ValueTask ExecuteAsync(object? parameter = null, CancellationToken cancellationToken = default)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        IsExecuting = true;
        ExecutionError = null;
        try
        {
            await execute(parameter, cancellationToken);
        }
        catch (Exception exception)
        {
            ExecutionError = exception;
            throw;
        }
        finally
        {
            IsExecuting = false;
        }
    }

    public async void Execute(object? parameter)
    {
        try
        {
            await ExecuteAsync(parameter);
        }
        catch
        {
            // The exception is exposed through ExecutionError for UI/status handling.
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
