namespace ChapterTool.Core.Importing.Disc.MovieObject;

internal static class MovieObjectParseLimits
{
    internal const int MaximumFileLength = 16 * 1024 * 1024;
    internal const int MaximumSectionLength = 16 * 1024 * 1024;
    internal const int MaximumObjects = 4096;
    internal const int MaximumCommandsPerObject = 65535;
    internal const int MaximumCommands = 1_000_000;

    internal static void ValidateAddress(uint address, long length, string name)
    {
        if (address != 0 && (address < 40 || address > length))
        {
            throw new InvalidDataException($"MovieObject {name} address {address} is outside the input.");
        }
    }
}
