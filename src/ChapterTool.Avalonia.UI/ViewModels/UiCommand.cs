using System.ComponentModel;
using System.Windows.Input;

namespace ChapterTool.Avalonia.UI.ViewModels;

public sealed class UiCommand(
    Func<object?, CancellationToken, ValueTask> execute,
    Func<object?, bool>? canExecute = null)
    : ICommand, INotifyPropertyChanged
{
    private const string UiErrorHandledKey = "ChapterTool.UiCommand.ErrorHandled";
    private readonly Func<object?, bool> canExecute = canExecute ?? (_ => true);

    public UiCommand(Func<object?, ValueTask> execute, Func<object?, bool>? canExecute = null)
        : this((parameter, _) => execute(parameter), canExecute)
    {
    }

    public event EventHandler? CanExecuteChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets or sets optional host boundary for unexpected command failures.</summary>
    public Func<Exception, ValueTask>? ErrorHandler { get; set; }

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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            ExecutionError = exception;
            if (ErrorHandler is not null)
            {
                exception.Data[UiErrorHandledKey] = true;
                await ErrorHandler(exception);
            }

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

    internal static bool WasReportedToUiBoundary(Exception exception) =>
        exception.Data[UiErrorHandledKey] is true;
}
