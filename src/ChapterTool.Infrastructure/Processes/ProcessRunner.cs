using System.Diagnostics;
using System.Text;
using ChapterTool.Infrastructure.Services;

namespace ChapterTool.Infrastructure.Processes;

public sealed class ProcessRunner : IProcessRunner
{
    private static readonly TimeSpan KillWaitTimeout = TimeSpan.FromSeconds(2);

    public async ValueTask<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken)
    {
        using var process = CreateProcess(request);
        using var timeoutCts = request.Timeout is null ? null : new CancellationTokenSource(request.Timeout.Value);
        using var linkedCts = timeoutCts is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var stdoutTask = Task.FromResult(new CapturedOutput(string.Empty, false));
        var stderrTask = Task.FromResult(new CapturedOutput(string.Empty, false));

        try
        {
            process.Start();
            stdoutTask = request.RedirectOutput
                ? ReadBoundedAsync(process.StandardOutput, request.MaxOutputCharacters)
                : stdoutTask;
            stderrTask = request.RedirectOutput
                ? ReadBoundedAsync(process.StandardError, request.MaxOutputCharacters)
                : stderrTask;

            await process.WaitForExitAsync(linkedCts.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            return new ProcessRunResult(
                process.ExitCode,
                stdout.Text,
                stderr.Text,
                TimedOut: false,
                Cancelled: false,
                request.FileName,
                request.Arguments,
                request.WorkingDirectory,
                stdout.Truncated || stderr.Truncated);
        }
        catch (OperationCanceledException)
        {
            KillProcess(process);
            await WaitForKilledProcessAsync(process);
            var stdout = await CaptureCompletedOutputAsync(stdoutTask);
            var stderr = await CaptureCompletedOutputAsync(stderrTask);
            var timedOut = timeoutCts?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested;
            return new ProcessRunResult(
                ExitCode: null,
                StandardOutput: stdout.Text,
                StandardError: stderr.Text,
                TimedOut: timedOut,
                Cancelled: !timedOut,
                request.FileName,
                request.Arguments,
                request.WorkingDirectory,
                stdout.Truncated || stderr.Truncated);
        }
    }

    private static Process CreateProcess(ProcessRunRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            WorkingDirectory = request.WorkingDirectory ?? string.Empty,
            RedirectStandardOutput = request.RedirectOutput,
            RedirectStandardError = request.RedirectOutput,
            UseShellExecute = false,
            CreateNoWindow = request.CreateNoWindow
        };
        if (request.RedirectOutput)
        {
            startInfo.StandardOutputEncoding = Encoding.UTF8;
            startInfo.StandardErrorEncoding = Encoding.UTF8;
        }

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return new Process { StartInfo = startInfo };
    }

    private static void KillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static async Task<CapturedOutput> CaptureCompletedOutputAsync(Task<CapturedOutput> outputTask)
    {
        try
        {
            return await outputTask.WaitAsync(KillWaitTimeout);
        }
        catch (Exception exception) when (exception is TimeoutException or IOException or ObjectDisposedException or InvalidOperationException)
        {
            return new CapturedOutput(string.Empty, false);
        }
    }

    private static async Task WaitForKilledProcessAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync(CancellationToken.None).WaitAsync(KillWaitTimeout);
        }
        catch (Exception exception) when (exception is TimeoutException or InvalidOperationException)
        {
        }
    }

    private static async Task<CapturedOutput> ReadBoundedAsync(TextReader reader, int maxCharacters)
    {
        maxCharacters = Math.Max(0, maxCharacters);
        var builder = new StringBuilder(capacity: Math.Min(maxCharacters, 4096));
        var buffer = new char[4096];
        var truncated = false;

        while (true)
        {
            var read = await reader.ReadAsync(buffer);
            if (read == 0)
            {
                break;
            }

            var remaining = maxCharacters - builder.Length;
            if (remaining > 0)
            {
                builder.Append(buffer, 0, Math.Min(read, remaining));
            }

            if (read > remaining)
            {
                truncated = true;
            }
        }

        return new CapturedOutput(builder.ToString(), truncated);
    }

    private sealed record CapturedOutput(string Text, bool Truncated);
}
