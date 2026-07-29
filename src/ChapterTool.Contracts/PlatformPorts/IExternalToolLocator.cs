namespace ChapterTool.Contracts.PlatformPorts;

public interface IExternalToolLocator
{
    ValueTask<ExternalToolLocation> LocateAsync(string toolId, CancellationToken cancellationToken);
}
