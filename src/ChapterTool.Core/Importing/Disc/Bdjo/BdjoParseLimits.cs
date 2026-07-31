namespace ChapterTool.Core.Importing.Disc.Bdjo;

internal static class BdjoParseLimits
{
    internal const int MaximumFileLength = 16 * 1024 * 1024;
    internal const int MaximumSectionLength = 4 * 1024 * 1024;
    internal const int MaximumPlaylists = 2047;
    internal const int MaximumCacheItems = 255;
    internal const int MaximumApplications = 255;
    internal const int MaximumProfiles = 15;
    internal const int MaximumNames = 4096;
    internal const int MaximumParameters = 255;
    internal const int MaximumStringLength = 255;
    internal const int MaximumStringDataLength = 65535;
}
