using System.Windows.Input;

namespace ChapterTool.Avalonia.ViewModels;

public sealed class UiCommand : ICommand
{
    private readonly Func<object?, CancellationToken, ValueTask> execute;
    private readonly Func<object?, bool> canExecute;

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

    public bool CanExecute(object? parameter = null) => canExecute(parameter);

    public ValueTask ExecuteAsync(object? parameter = null, CancellationToken cancellationToken = default)
    {
        if (!CanExecute(parameter))
        {
            return ValueTask.CompletedTask;
        }

        return execute(parameter, cancellationToken);
    }

    public async void Execute(object? parameter)
    {
        await ExecuteAsync(parameter);
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
