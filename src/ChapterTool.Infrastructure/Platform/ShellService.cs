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
}
