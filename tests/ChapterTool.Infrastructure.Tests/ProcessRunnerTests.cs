using ChapterTool.Core.Services;
using ChapterTool.Infrastructure.Processes;

namespace ChapterTool.Infrastructure.Tests;

public sealed class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_captures_stdout_stderr_exit_code_command_and_working_directory()
    {
        var runner = new ProcessRunner();
        var workingDirectory = Path.GetTempPath();

        var request = ShellCommand.Create(
            "echo standard output && echo standard error 1>&2 && exit 7",
            workingDirectory);

        var result = await runner.RunAsync(request, CancellationToken.None);

        Assert.Equal(7, result.ExitCode);
        Assert.Contains("standard output", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("standard error", result.StandardError, StringComparison.Ordinal);
        Assert.False(result.TimedOut);
        Assert.False(result.Cancelled);
        Assert.Equal(request.FileName, result.FileName);
        Assert.Equal(workingDirectory, result.WorkingDirectory);
    }

    [Fact]
    public async Task RunAsync_decodes_non_ascii_stdout_and_stderr()
    {
        var runner = new ProcessRunner();
        var request = ShellCommand.CreateUtf8Output("章節", "错误");

        var result = await runner.RunAsync(request, CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("章節", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("错误", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_marks_timeout_and_kills_process()
    {
        var runner = new ProcessRunner();

        var result = await runner.RunAsync(
            ShellCommand.CreateSleep(TimeSpan.FromSeconds(5), timeout: TimeSpan.FromMilliseconds(100)),
            CancellationToken.None);

        Assert.Null(result.ExitCode);
        Assert.True(result.TimedOut);
        Assert.False(result.Cancelled);
    }

    [Fact]
    public async Task RunAsync_marks_cancellation()
    {
        var runner = new ProcessRunner();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var result = await runner.RunAsync(
            ShellCommand.CreateSleep(TimeSpan.FromSeconds(5)),
            cts.Token);

        Assert.Null(result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.True(result.Cancelled);
    }

    private static class ShellCommand
    {
        public static ProcessRunRequest Create(string command, string? workingDirectory = null, TimeSpan? timeout = null)
        {
            if (OperatingSystem.IsWindows())
            {
                return new ProcessRunRequest("cmd.exe", ["/c", command], workingDirectory, timeout);
            }

            return new ProcessRunRequest("/bin/sh", ["-c", command], workingDirectory, timeout);
        }

        public static ProcessRunRequest CreateSleep(TimeSpan duration, TimeSpan? timeout = null)
        {
            if (OperatingSystem.IsWindows())
            {
                return Create($"ping 127.0.0.1 -n {Math.Max(2, (int)duration.TotalSeconds + 1)} > nul", timeout: timeout);
            }

            return Create($"sleep {Math.Max(1, (int)duration.TotalSeconds)}", timeout: timeout);
        }

        public static ProcessRunRequest CreateUtf8Output(string stdout, string stderr)
        {
            if (OperatingSystem.IsWindows())
            {
                return new ProcessRunRequest(
                    "powershell.exe",
                    ["-NoProfile", "-Command", $"$OutputEncoding = [System.Text.Encoding]::UTF8; [Console]::Error.WriteLine('{stderr}'); [Console]::WriteLine('{stdout}')"],
                    null);
            }

            return Create($"printf '{stdout}\\n'; printf '{stderr}\\n' 1>&2");
        }
    }
}
