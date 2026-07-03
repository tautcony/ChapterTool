namespace ChapterTool.Infrastructure.Platform;

public interface IPrivilegeService
{
    bool IsAdministrator { get; }

    ValueTask<PrivilegeResult> RequestElevationAsync(string operationId, CancellationToken cancellationToken);
}
