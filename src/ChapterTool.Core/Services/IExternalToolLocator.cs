namespace ChapterTool.Core.Services;

public interface IExternalToolLocator
{
    ValueTask<ExternalToolLocation> LocateAsync(string toolId, CancellationToken cancellationToken);
}
