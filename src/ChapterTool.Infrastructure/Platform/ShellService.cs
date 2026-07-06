using System.Diagnostics;
using ChapterTool.Core.Services;

namespace ChapterTool.Infrastructure.Platform;

public sealed class ShellService : IShellService
{
    public ValueTask OpenAsync(string target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var _ = Process.Start(new ProcessStartInfo
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
        catch
        {
            // Best-effort: silently ignore failures when platform tool is unavailable.
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
                if (TryRun("wt", "-d", directoryPath))
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
                TryRun("x-terminal-emulator", "--working-directory", directoryPath);
            }
        }
        catch
        {
            // Best-effort: silently ignore failures when platform tool is unavailable.
        }

        return ValueTask.CompletedTask;
    }

    private static void Run(string fileName, params string[] arguments)
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

    private static void Run(ProcessStartInfo startInfo)
    {
        using var process = Process.Start(startInfo);
    }

    private static bool TryRun(string fileName, params string[] arguments)
    {
        try
        {
            Run(fileName, arguments);
            return true;
        }
        catch
        {
            return false;
        }
    }

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
