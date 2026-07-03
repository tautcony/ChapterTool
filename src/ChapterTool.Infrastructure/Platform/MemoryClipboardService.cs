using ChapterTool.Core.Services;

namespace ChapterTool.Infrastructure.Platform;

public sealed class MemoryClipboardService : IClipboardService
{
    private string? text;

    public ValueTask<string?> GetTextAsync(CancellationToken cancellationToken) => ValueTask.FromResult(text);

    public ValueTask SetTextAsync(string value, CancellationToken cancellationToken)
    {
        this.text = value;
        return ValueTask.CompletedTask;
    }
}
