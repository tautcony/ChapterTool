using ChapterTool.Avalonia.ViewModels;

namespace ChapterTool.Avalonia.Views;

/// <summary>Converts unexpected asynchronous UI exceptions into application status and log state.</summary>
internal sealed class UiOperationBoundary(Func<Exception, ValueTask> reportException)
{
    public async Task RunAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (OperationCanceledException)
        {
            // Cancellation is a normal workflow outcome.
        }
        catch (Exception exception) when (!UiCommand.WasReportedToUiBoundary(exception))
        {
            await reportException(exception);
        }
    }
}
