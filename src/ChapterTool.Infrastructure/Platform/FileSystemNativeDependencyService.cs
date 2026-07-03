namespace ChapterTool.Infrastructure.Platform;

public sealed class FileSystemNativeDependencyService(IReadOnlyList<string> searchDirectories) : INativeDependencyService
{
    public ValueTask<NativeDependencyLocation> ResolveAsync(string dependencyId, CancellationToken cancellationToken)
    {
        foreach (var directory in searchDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.Combine(directory, dependencyId);
            if (File.Exists(path))
            {
                return ValueTask.FromResult(new NativeDependencyLocation(true, path));
            }
        }

        return ValueTask.FromResult(new NativeDependencyLocation(
            false,
            null,
            "NativeLibraryMissing",
            $"Native dependency '{dependencyId}' was not found."));
    }

}
