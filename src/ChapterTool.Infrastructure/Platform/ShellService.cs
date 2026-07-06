using System.Diagnostics;
using ChapterTool.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChapterTool.Infrastructure.Platform;

public sealed class ShellService : IShellService
{
    private readonly ILogger<ShellService> logger;
    private readonly Func<ProcessStartInfo, Process?> startProcess;

    public ShellService(ILogger<ShellService>? logger = null)
        : this(logger, Process.Start)
    {
    }

    internal ShellService(ILogger<ShellService>? logger, Func<ProcessStartInfo, Process?> startProcess)
    {
        this.logger = logger ?? NullLogger<ShellService>.Instance;
        this.startProcess = startProcess;
    }

    public ValueTask OpenAsync(string target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var _ = Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true
        });

        return ValueTask.CompletedTask;
    }

    public ValueTask RevealInFolderAsync(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // explorer /select,"path" highlights the file in Explorer
                Run("explorer", $"/select,\"{filePath}\"");
            }
            else if (OperatingSystem.IsMacOS())
            {
                Run("open", "-R", filePath);
            }
            else
            {
                // Linux: open the parent directory
                var dir = Path.GetDirectoryName(filePath) ?? filePath;
                Run("xdg-open", dir);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to reveal '{FilePath}' in the platform file manager.", filePath);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask OpenTerminalAsync(string directoryPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // Prefer Windows Terminal, fall back to cmd
                if (TryRun("wt", out _, "-d", directoryPath))
                {
                    return ValueTask.CompletedTask;
                }

                Run(CreateWindowsCommandPromptStartInfo(directoryPath));
            }
            else if (OperatingSystem.IsMacOS())
            {
                Run("open", "-a", "Terminal", directoryPath);
            }
            else
            {
                // Try common terminal emulators
                if (!TryRun("x-terminal-emulator", out var exception, "--working-directory", directoryPath))
                {
                    logger.LogWarning(exception, "Unable to open a terminal in '{DirectoryPath}'.", directoryPath);
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to open a terminal in '{DirectoryPath}'.", directoryPath);
        }

        return ValueTask.CompletedTask;
    }

    private void Run(string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        Run(startInfo);
    }

    private void Run(ProcessStartInfo startInfo)
    {
        using var process = Start(startInfo);
    }

    private bool TryRun(string fileName, out Exception? exception, params string[] arguments)
    {
        try
        {
            Run(fileName, arguments);
            exception = null;
            return true;
        }
        catch (Exception caught)
        {
            exception = caught;
            return false;
        }
    }

    private Process Start(ProcessStartInfo startInfo) =>
        startProcess(startInfo) ?? throw new InvalidOperationException($"Unable to start process '{startInfo.FileName}'.");

    internal static ProcessStartInfo CreateWindowsCommandPromptStartInfo(string directoryPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            WorkingDirectory = directoryPath,
            UseShellExecute = false,
            CreateNoWindow = false
        };
        startInfo.ArgumentList.Add("/k");
        return startInfo;
    }
}
