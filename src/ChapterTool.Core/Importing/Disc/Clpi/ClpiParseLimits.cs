namespace ChapterTool.Core.Importing.Disc.Clpi;

internal static class ClpiParseLimits
{
    internal const int MaximumClipInfoLength = 64 * 1024;
    internal const int MaximumSequenceInfoLength = 256 * 1024;
    internal const int MaximumProgramInfoLength = 256 * 1024;
    internal const int MaximumCPILength = 4 * 1024 * 1024;
    internal const int MaximumStreamPIDEntries = 256;
    internal const int MaximumEPCoarseEntries = 65536;
    internal const int MaximumEPFineEntries = 262144;
    internal const int MaximumATCSequences = 64;
    internal const int MaximumSTCSequences = 64;
    internal const int MaximumPrograms = 1024;
    internal const int MaximumStreamsInPS = 256;
    internal const int MaximumExtensionDataLength = 16 * 1024 * 1024;

    internal static void ValidateCount(int count, int maximumCount, string itemName)
    {
        if (count < 0 || count > maximumCount)
        {
            throw new InvalidDataException($"CLPI {itemName} count {count} exceeds the supported maximum of {maximumCount}.");
        }
    }

    internal static void ValidateCountByBudget(int count, int minimumEntryBytes, long remainingBytes, string itemName)
    {
        if (count < 0 || minimumEntryBytes < 0 || remainingBytes < 0 ||
            (minimumEntryBytes > 0 && count > remainingBytes / minimumEntryBytes))
        {
            throw new InvalidDataException($"CLPI {itemName} count {count} cannot fit in the remaining container budget.");
        }
    }

    internal static void ValidateCountByBudget(uint count, int minimumEntryBytes, long remainingBytes, string itemName)
    {
        if (minimumEntryBytes < 0 || remainingBytes < 0 ||
            (minimumEntryBytes > 0 && count > (ulong)(remainingBytes / minimumEntryBytes)))
        {
            throw new InvalidDataException($"CLPI {itemName} count {count} cannot fit in the remaining container budget.");
        }
    }
}
