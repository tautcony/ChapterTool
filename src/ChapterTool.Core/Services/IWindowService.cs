namespace ChapterTool.Core.Services;

public interface IWindowService
{
    ValueTask ShowAsync(string windowId, object? parameter, CancellationToken cancellationToken);

    ValueTask HideAsync(string windowId, CancellationToken cancellationToken);
}
