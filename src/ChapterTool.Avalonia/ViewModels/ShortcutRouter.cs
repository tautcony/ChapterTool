namespace ChapterTool.Avalonia.ViewModels;

public sealed class ShortcutRouter(MainWindowViewModel viewModel)
{
    public ValueTask RouteAsync(string gesture, CancellationToken cancellationToken = default)
    {
        return gesture switch
        {
            "Ctrl+O" => viewModel.LoadCommand.ExecuteAsync(cancellationToken: cancellationToken),
            "Ctrl+S" => viewModel.SaveCommand.ExecuteAsync(cancellationToken: cancellationToken),
            "Ctrl+R" or "F5" => viewModel.ReloadCommand.ExecuteAsync(cancellationToken: cancellationToken),
            "Ctrl+L" => viewModel.LogCommand.ExecuteAsync(cancellationToken: cancellationToken),
            "F11" => viewModel.PreviewCommand.ExecuteAsync(cancellationToken: cancellationToken),
            "Ctrl+0" => viewModel.SelectClipCommand.ExecuteAsync(9, cancellationToken),
            _ when gesture.StartsWith("Ctrl+", StringComparison.Ordinal) && int.TryParse(gesture["Ctrl+".Length..], out var index) =>
                viewModel.SelectClipCommand.ExecuteAsync(index - 1, cancellationToken),
            _ => ValueTask.CompletedTask
        };
    }
}
