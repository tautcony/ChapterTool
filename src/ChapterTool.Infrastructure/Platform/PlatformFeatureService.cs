using ChapterTool.Core.Services;

namespace ChapterTool.Infrastructure.Platform;

public sealed class PlatformFeatureService : IPlatformFeatureService
{
    public bool IsWindows => OperatingSystem.IsWindows();

    public bool SupportsFileAssociation => IsWindows;

    public bool SupportsPrivilegeElevation => IsWindows;
}
