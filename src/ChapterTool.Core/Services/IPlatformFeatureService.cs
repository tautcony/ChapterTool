namespace ChapterTool.Core.Services;

public interface IPlatformFeatureService
{
    bool IsWindows { get; }

    bool SupportsFileAssociation { get; }

    bool SupportsPrivilegeElevation { get; }
}
