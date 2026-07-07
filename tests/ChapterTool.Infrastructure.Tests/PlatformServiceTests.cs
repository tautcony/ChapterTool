using ChapterTool.Core.Services;
using ChapterTool.Infrastructure.Platform;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Infrastructure.Tests;

public sealed class PlatformServiceTests
{
    [Fact]
    public async Task Native_dependency_service_reports_missing_dependency()
    {
        var service = new FileSystemNativeDependencyService([]);

        var result = await service.ResolveAsync("missing-tool", TestContext.Current.CancellationToken);

        Assert.False(result.Found);
        Assert.Equal("NativeLibraryMissing", result.DiagnosticCode);
    }

    [Fact]
    public async Task Memory_clipboard_dialog_localization_and_window_services_are_testable_skeletons()
    {
        var clipboard = new MemoryClipboardService();
        await clipboard.SetTextAsync("copied", TestContext.Current.CancellationToken);
        Assert.Equal("copied", await clipboard.GetTextAsync(TestContext.Current.CancellationToken));

        var dialogs = new ScriptedDialogService(new DialogResult(true, "accepted"));
        var dialogResult = await dialogs.ShowMessageAsync(
            new DialogRequest("title", "message", DialogKind.Confirmation),
            TestContext.Current.CancellationToken);
        Assert.True(dialogResult.Accepted);
        Assert.Equal("accepted", dialogResult.Text);

        var windows = new RecordingWindowService();
        await windows.ShowAsync("preview", "text", TestContext.Current.CancellationToken);
        await windows.HideAsync("preview", TestContext.Current.CancellationToken);
        Assert.Equal(["show:preview", "hide:preview"], windows.Calls);
    }

    [Fact]
    public void Windows_terminal_fallback_keeps_directory_out_of_command_arguments()
    {
        const string directory = "C:\\Temp & calc \"quoted\"";

        var startInfo = ShellService.CreateWindowsCommandPromptStartInfo(directory);

        Assert.Equal("cmd.exe", startInfo.FileName);
        Assert.Equal(directory, startInfo.WorkingDirectory);
        Assert.Equal(["/k"], startInfo.ArgumentList);
    }

    [Fact]
    public async Task Shell_service_logs_reveal_failures()
    {
        var logger = new RecordingLogger<ShellService>();
        var service = new ShellService(logger, _ => throw new InvalidOperationException("launcher unavailable"));

        await service.RevealInFolderAsync("missing.mkv", TestContext.Current.CancellationToken);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.Contains("missing.mkv", entry.Message, StringComparison.Ordinal);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
}
