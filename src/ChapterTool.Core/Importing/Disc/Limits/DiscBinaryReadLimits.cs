namespace ChapterTool.Core.Importing.Disc.Limits;

/// <summary>Shared defensive limits for untrusted disc binary reads.</summary>
internal static class DiscBinaryReadLimits
{
    internal const int MaximumExactReadBytes = 64 * 1024 * 1024;
}
