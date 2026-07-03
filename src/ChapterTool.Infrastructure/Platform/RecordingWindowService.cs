using ChapterTool.Core.Services;

namespace ChapterTool.Infrastructure.Platform;

public sealed class RecordingWindowService : IWindowService
{
    private readonly List<string> calls = [];

    public IReadOnlyList<string> Calls => calls;

    public IReadOnlyDictionary<string, object?> VisibleWindows => visibleWindows;

    private readonly Dictionary<string, object?> visibleWindows = [];

    public ValueTask ShowAsync(string windowId, object? parameter, CancellationToken cancellationToken)
    {
        calls.Add($"show:{windowId}");
        visibleWindows[windowId] = parameter;
        return ValueTask.CompletedTask;
    }

    public ValueTask HideAsync(string windowId, CancellationToken cancellationToken)
    {
        calls.Add($"hide:{windowId}");
        visibleWindows.Remove(windowId);
        return ValueTask.CompletedTask;
    }
}
