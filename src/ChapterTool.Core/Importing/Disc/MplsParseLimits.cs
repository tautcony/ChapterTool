namespace ChapterTool.Core.Importing.Disc;

/// <summary>Finite limits for MPLS container lengths and collection counts.</summary>
internal static class MplsParseLimits
{
    internal const int MaximumAppInfoLength = 64 * 1024;
    internal const int MaximumPlaylistLength = 16 * 1024 * 1024;
    internal const int MaximumPlayItemLength = 1 * 1024 * 1024;
    internal const int MaximumSubPathLength = 1 * 1024 * 1024;
    internal const int MaximumSubPlayItemLength = 1 * 1024 * 1024;
    internal const int MaximumStnTableLength = 1 * 1024 * 1024;
    internal const int MaximumMarkTableLength = 4 * 1024 * 1024;
    internal const int MaximumExtensionDataLength = 16 * 1024 * 1024;
    internal const int MaximumPlayItems = 4096;
    internal const int MaximumSubPaths = 1024;
    internal const int MaximumSubPlayItems = 1024;
    internal const int MaximumMultiAngleEntries = 64;
    internal const int MaximumMultiClipEntries = 128;
    internal const int MaximumStreamEntriesPerCategory = 256;
    internal const int MaximumPlayListMarks = 8192;
    internal const int MaximumExtensionEntries = 1024;

    internal static void ValidateContainerLength(uint length, int minimumLength, int maximumLength, string containerName)
    {
        if (length < minimumLength || length > maximumLength)
        {
            throw new InvalidDataException($"MPLS {containerName} length {length} is outside the supported range {minimumLength}..{maximumLength}.");
        }
    }

    internal static void ValidateCount(int count, int maximumCount, string itemName)
    {
        if (count < 0 || count > maximumCount)
        {
            throw new InvalidDataException($"MPLS {itemName} count {count} exceeds the supported maximum of {maximumCount}.");
        }
    }

    internal static void ValidateCountByBudget(int count, int minimumEntryBytes, long remainingBytes, string itemName)
    {
        if (count < 0 || minimumEntryBytes < 0 || remainingBytes < 0 ||
            (minimumEntryBytes > 0 && count > remainingBytes / minimumEntryBytes))
        {
            throw new InvalidDataException($"MPLS {itemName} count {count} cannot fit in the remaining container budget.");
        }
    }

    internal static void SeekToAddress(Stream stream, uint address, string sectionName)
    {
        if (!stream.CanSeek || address > stream.Length)
        {
            throw new InvalidDataException($"MPLS {sectionName} address {address} is outside the input stream.");
        }

        stream.Position = address;
    }
}

internal static class MplsContainerReadExtensions
{
    internal static void SkipContainerRemainder(this Stream stream, long contentStart, uint length, string containerName)
    {
        var consumed = stream.Position - contentStart;
        if (consumed < 0 || consumed > length)
        {
            throw new InvalidDataException($"MPLS {containerName} length {length} is smaller than its consumed content.");
        }

        stream.SkipBytes(length - consumed);
    }
}
