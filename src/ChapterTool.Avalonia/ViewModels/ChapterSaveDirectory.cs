using ChapterTool.Core.Exporting;

namespace ChapterTool.Avalonia.ViewModels;

/// <summary>
/// Resolves the effective chapter output directory from override, configured, or source paths.
/// </summary>
internal static class ChapterSaveDirectory
{
    public static string? Resolve(string? directoryOverride, string? configuredDirectory, string? sourcePath)
    {
        if (!string.IsNullOrWhiteSpace(directoryOverride))
        {
            return ChapterSavePath.TryNormalizeDirectory(directoryOverride, out var overrideDirectory)
                ? overrideDirectory
                : null;
        }

        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            return ChapterSavePath.TryNormalizeDirectory(configuredDirectory, out var configured)
                ? configured
                : null;
        }

        return ChapterSavePath.DirectoryOfSourcePath(sourcePath);
    }
}
