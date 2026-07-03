namespace ChapterTool.Infrastructure.Platform;

public interface INativeDependencyService
{
    ValueTask<NativeDependencyLocation> ResolveAsync(string dependencyId, CancellationToken cancellationToken);
}
