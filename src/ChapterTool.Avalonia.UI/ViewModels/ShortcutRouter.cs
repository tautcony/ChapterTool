namespace ChapterTool.Avalonia.UI.ViewModels;

using ChapterTool.Contracts.Shortcuts;

public sealed class ShortcutRouter(
    MainWindowViewModel viewModel,
    ShortcutMapping? mapping = null,
    Func<ValueTask>? load = null,
    Func<string, ValueTask>? navigateClip = null)
{
    private ShortcutMapping mapping = mapping ?? ShortcutMapping.Default;

    public void UpdateMapping(ShortcutMapping next) => mapping = next ?? throw new ArgumentNullException(nameof(next));

    public ValueTask RouteAsync(string gesture, CancellationToken cancellationToken = default)
    {
        var action = mapping.ActionForGesture(gesture);
        return action switch
        {
            ShortcutCatalog.SaveId => viewModel.SaveCommand.ExecuteAsync(cancellationToken: cancellationToken),
            ShortcutCatalog.ReloadId => viewModel.ReloadCommand.ExecuteAsync(cancellationToken: cancellationToken),
            ShortcutCatalog.LogId => viewModel.LogCommand.ExecuteAsync(cancellationToken: cancellationToken),
            ShortcutCatalog.PreviewId => viewModel.PreviewCommand.ExecuteAsync(cancellationToken: cancellationToken),
            ShortcutCatalog.LoadId when load is not null => load(),
            ShortcutCatalog.PreviousClipId when navigateClip is not null => navigateClip("PageUp"),
            ShortcutCatalog.NextClipId when navigateClip is not null => navigateClip("PageDown"),
            _ => ValueTask.CompletedTask
        };
    }

}
