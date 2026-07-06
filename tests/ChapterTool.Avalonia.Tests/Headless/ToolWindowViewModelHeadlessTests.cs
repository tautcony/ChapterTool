using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Infrastructure.Platform;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class ToolWindowViewModelHeadlessTests
{
    [AvaloniaFact]
    public void TextToolRefreshesWhenLiveLogServiceAddsEntry()
    {
        var logService = new ApplicationLogPanelProvider();
        var logger = logService.CreateLogger("ChapterTool.Tests");
        var vm = new TextToolViewModel(
            () => logService.Format(),
            new TextToolOptions { LiveRefreshService = logService });

        logger.LogInformation("Live event");
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Live event", vm.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task TextToolMarshalsLiveLogRefreshFromBackgroundThread()
    {
        var logService = new ApplicationLogPanelProvider();
        var logger = logService.CreateLogger("ChapterTool.Tests");
        var threadIds = new List<int>();
        var vm = new TextToolViewModel(
            () =>
            {
                threadIds.Add(Environment.CurrentManagedThreadId);
                return logService.Format();
            },
            new TextToolOptions { LiveRefreshService = logService });
        var uiThreadId = Environment.CurrentManagedThreadId;

        await Task.Run(() => logger.LogInformation("Background event"));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Background event", vm.Text, StringComparison.Ordinal);
        Assert.Equal(uiThreadId, threadIds[^1]);
    }
}
