namespace ChapterTool.Contracts.PlatformPorts;

public interface IClipboardService
{
    ValueTask<string?> GetTextAsync(CancellationToken cancellationToken);

    ValueTask SetTextAsync(string value, CancellationToken cancellationToken);
}
