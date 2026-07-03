namespace ChapterTool.Core.Services;

public interface IShellService
{
    ValueTask OpenAsync(string target, CancellationToken cancellationToken);
}
