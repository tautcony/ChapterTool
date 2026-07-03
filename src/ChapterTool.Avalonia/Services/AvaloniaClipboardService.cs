using Avalonia.Controls;
using Avalonia.Input.Platform;
using ChapterTool.Core.Services;

namespace ChapterTool.Avalonia.Services;

public sealed class AvaloniaClipboardService(Window owner) : IClipboardService
{
    public async ValueTask<string?> GetTextAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return owner.Clipboard is null ? null : await owner.Clipboard.TryGetTextAsync();
    }

    public async ValueTask SetTextAsync(string value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (owner.Clipboard is not null)
        {
            await owner.Clipboard.SetTextAsync(value);
        }
    }
}
