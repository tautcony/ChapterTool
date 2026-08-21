namespace ChapterTool.Core.Importing.Disc.Index;

internal static class IndexParseLimits
{
    internal const int MinimumIndexesLength = IndexTitleEntry.SerializedLength * 2 + sizeof(ushort);
    internal const int MaximumIndexesLength = 256 * 1024;
    internal const int MaximumAppInfoLength = 64 * 1024;
    internal const int MaximumTitles = 4096;
    internal const int MaximumExtensionLength = 16 * 1024 * 1024;
    internal const int MaximumExtensions = 255;
}
