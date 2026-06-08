using System.Windows.Input;
using System.ComponentModel;

namespace ChapterTool.Avalonia.ViewModels;

public sealed class UiCommand : ICommand, INotifyPropertyChanged
{
    private readonly Func<object?, CancellationToken, ValueTask> execute;
    private readonly Func<object?, bool> canExecute;
    private bool isExecuting;
    private Exception? executionError;

    public UiCommand(Func<object?, CancellationToken, ValueTask> execute, Func<object?, bool>? canExecute = null)
    {
        this.execute = execute;
        this.canExecute = canExecute ?? (_ => true);
    }

    public UiCommand(Func<object?, ValueTask> execute, Func<object?, bool>? canExecute = null)
        : this((parameter, _) => execute(parameter), canExecute)
    {
    }

    public event EventHandler? CanExecuteChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsExecuting
    {
        get => isExecuting;
        private set
        {
            if (isExecuting == value)
            {
                return;
            }

            isExecuting = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExecuting)));
            RaiseCanExecuteChanged();
        }
    }

    public Exception? ExecutionError
    {
        get => executionError;
        private set
        {
            if (ReferenceEquals(executionError, value))
            {
                return;
            }

            executionError = value;
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
