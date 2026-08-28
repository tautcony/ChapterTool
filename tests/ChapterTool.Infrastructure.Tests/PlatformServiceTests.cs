using System.Diagnostics;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Infrastructure.Platform;
using ChapterTool.Infrastructure.Services;
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
        Assert.Equal(ChapterDiagnosticCode.NativeLibraryMissing, result.DiagnosticCode);
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
    public async Task Shell_service_sends_windows_reveal_as_single_select_argument()
    {
        ProcessStartInfo? captured = null;
        var service = new ShellService(null, startInfo =>
        {
            captured = startInfo;
            return null;
        });

        if (!OperatingSystem.IsWindows())
        {
            await service.RevealInFolderAsync(@"C:\My File.mkv", TestContext.Current.CancellationToken);
            return;
        }

        await service.RevealInFolderAsync(@"C:\My File.mkv", TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal("explorer", captured.FileName);
        Assert.Equal(["/select,C:\\My File.mkv"], captured.ArgumentList);
    }

    [Fact]
    public async Task Shell_service_logs_open_failures()
    {
        var logger = new RecordingLogger<ShellService>();
        var service = new ShellService(logger, _ => throw new InvalidOperationException("launcher unavailable"));

        await service.OpenAsync("missing.mkv", TestContext.Current.CancellationToken);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("missing.mkv", entry.Message, StringComparison.Ordinal);
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

    [Fact]
    public async Task Shell_service_opens_successfully_without_logging()
    {
        var logger = new RecordingLogger<ShellService>();
        var service = new ShellService(logger, _ => new Process());

        await service.OpenAsync("file.mkv", TestContext.Current.CancellationToken);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task Shell_service_reveals_in_folder_successfully()
    {
        ProcessStartInfo? captured = null;
        var service = new ShellService(null, startInfo =>
        {
            captured = startInfo;
            return new Process();
        });

        await service.RevealInFolderAsync("/media/file.mkv", TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        if (OperatingSystem.IsMacOS())
        {
            Assert.Equal("open", captured.FileName);
            Assert.Equal(["-R", "/media/file.mkv"], captured.ArgumentList);
        }
        else if (OperatingSystem.IsWindows())
        {
            Assert.Equal("explorer", captured.FileName);
        }
        else
        {
            Assert.Equal("xdg-open", captured.FileName);
        }
    }

    [Fact]
    public async Task Shell_service_opens_terminal_successfully()
    {
        ProcessStartInfo? captured = null;
        var service = new ShellService(null, startInfo =>
        {
            captured = startInfo;
            return new Process();
        });

        await service.OpenTerminalAsync("/workspace", TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        if (OperatingSystem.IsMacOS())
        {
            Assert.Equal("open", captured.FileName);
            Assert.Equal(["-a", "Terminal", "/workspace"], captured.ArgumentList);
        }
        else if (OperatingSystem.IsWindows())
        {
            Assert.Equal("wt", captured.FileName);
            Assert.Equal(["-d", "/workspace"], captured.ArgumentList);
        }
        else
        {
            Assert.Equal("x-terminal-emulator", captured.FileName);
            Assert.Equal(["--working-directory", "/workspace"], captured.ArgumentList);
        }
    }

    [Fact]
    public async Task Shell_service_falls_back_to_cmd_when_windows_terminal_unavailable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var calls = 0;
        var service = new ShellService(null, _ => calls++ == 0 ? null : new Process());

        await service.OpenTerminalAsync(@"C:\Temp", TestContext.Current.CancellationToken);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Shell_service_logs_terminal_failure_on_linux()
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            return;
        }

        var logger = new RecordingLogger<ShellService>();
        var service = new ShellService(logger, _ => null);

        await service.OpenTerminalAsync("/workspace", TestContext.Current.CancellationToken);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("/workspace", entry.Message, StringComparison.Ordinal);
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

    private sealed class RecordingWindowService
    {
        private readonly List<string> calls = [];
        private readonly Dictionary<string, object?> visibleWindows = [];

        public IReadOnlyList<string> Calls => calls;

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
}
