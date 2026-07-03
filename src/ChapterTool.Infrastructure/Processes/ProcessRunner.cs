using System.Diagnostics;
using System.Text;
using ChapterTool.Core.Services;

namespace ChapterTool.Infrastructure.Processes;

public sealed class ProcessRunner : IProcessRunner
{
    public async ValueTask<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken)
    {
        using var process = CreateProcess(request);
        using var timeoutCts = request.Timeout is null ? null : new CancellationTokenSource(request.Timeout.Value);
        using var linkedCts = timeoutCts is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            process.Start();
            var stdoutTask = request.RedirectOutput
                ? process.StandardOutput.ReadToEndAsync(linkedCts.Token)
                : Task.FromResult(string.Empty);
            var stderrTask = request.RedirectOutput
                ? process.StandardError.ReadToEndAsync(linkedCts.Token)
                : Task.FromResult(string.Empty);

            await process.WaitForExitAsync(linkedCts.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            return new ProcessRunResult(
                process.ExitCode,
                stdout,
                stderr,
                TimedOut: false,
                Cancelled: false,
                request.FileName,
                request.Arguments,
                request.WorkingDirectory);
        }
        catch (OperationCanceledException)
        {
            KillProcess(process);
            var timedOut = timeoutCts?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested;
            return new ProcessRunResult(
                ExitCode: null,
                StandardOutput: string.Empty,
                StandardError: string.Empty,
                TimedOut: timedOut,
                Cancelled: !timedOut,
                request.FileName,
                request.Arguments,
                request.WorkingDirectory);
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
}
