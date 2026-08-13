using ChapterTool.Infrastructure.Processes;
using ChapterTool.Infrastructure.Services;

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

        var result = await runner.RunAsync(request, TestContext.Current.CancellationToken);

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

        var result = await runner.RunAsync(request, TestContext.Current.CancellationToken);

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
            TestContext.Current.CancellationToken);

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

    [Fact]
    public async Task RunAsync_truncates_large_redirected_output()
    {
        var runner = new ProcessRunner();
        var request = ShellCommand.CreateExactOutput("abcdef", "uvwxyz") with { MaxOutputCharacters = 3 };

        var result = await runner.RunAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("abc", result.StandardOutput);
        Assert.Equal("uvw", result.StandardError);
        Assert.True(result.OutputTruncated);
    }

    [Fact]
    public async Task RunAsync_preserves_partial_output_after_timeout()
    {
        var runner = new ProcessRunner();

        var result = await runner.RunAsync(
            ShellCommand.CreateOutputThenSleep("before-timeout", timeout: TimeSpan.FromSeconds(1)),
            TestContext.Current.CancellationToken);

        Assert.True(result.TimedOut);
        Assert.Contains("before-timeout", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_drains_output_after_parent_exits_without_waiting_for_grandchild()
    {
        var runner = new ProcessRunner();
        var started = DateTime.UtcNow;

        var result = await runner.RunAsync(
            ShellCommand.CreateOrphanedStdoutHolder("early-output"),
            TestContext.Current.CancellationToken);

        var elapsed = DateTime.UtcNow - started;
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.False(result.Cancelled);
        Assert.True(elapsed < TimeSpan.FromSeconds(6), $"Drain waited {elapsed}.");
        Assert.True(
            result.OutputTruncated || result.StandardOutput.Contains("early-output", StringComparison.Ordinal),
            $"stdout='{result.StandardOutput}', truncated={result.OutputTruncated}");
    }

    [Fact]
    public async Task RunAsync_can_disable_output_redirection()
    {
        var runner = new ProcessRunner();
        var request = ShellCommand.Create("echo hidden output", redirectOutput: false);

        var result = await runner.RunAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    private static class ShellCommand
    {
        public static ProcessRunRequest Create(string command, string? workingDirectory = null, TimeSpan? timeout = null, bool redirectOutput = true)
        {
            if (OperatingSystem.IsWindows())
            {
                return new ProcessRunRequest("cmd.exe", ["/c", command], workingDirectory, timeout, redirectOutput);
            }

            return new ProcessRunRequest("/bin/sh", ["-c", command], workingDirectory, timeout, redirectOutput);
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
                    ["-NoProfile", "-Command", $"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; [Console]::WriteLine('{stdout}'); [Console]::Error.WriteLine('{stderr}')"]);
            }

            return Create($@"printf '{stdout}\n'; printf '{stderr}\n' 1>&2");
        }

        public static ProcessRunRequest CreateExactOutput(string stdout, string stderr)
        {
            if (OperatingSystem.IsWindows())
            {
                return new ProcessRunRequest(
                    "powershell.exe",
                    ["-NoProfile", "-Command", $"[Console]::Write('{stdout}'); [Console]::Error.Write('{stderr}')"]);
            }

            return Create($"printf '{stdout}'; printf '{stderr}' 1>&2");
        }

        public static ProcessRunRequest CreateOutputThenSleep(string stderr, TimeSpan? timeout = null)
        {
            if (OperatingSystem.IsWindows())
            {
                return Create($"echo {stderr} 1>&2 & ping 127.0.0.1 -n 6 > nul", timeout: timeout);
            }

            return Create($"printf '{stderr}' 1>&2; sleep 5", timeout: timeout);
        }

        public static ProcessRunRequest CreateOrphanedStdoutHolder(string stdout)
        {
            if (OperatingSystem.IsWindows())
            {
                return Create($"echo {stdout} & start /b ping 127.0.0.1 -n 10 >nul");
            }

            return Create($"printf '{stdout}\\n'; sleep 10 &");
        }
    }
}
