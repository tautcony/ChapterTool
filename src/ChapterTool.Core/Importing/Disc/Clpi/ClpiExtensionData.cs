namespace ChapterTool.Core.Importing.Disc.Clpi;

internal sealed record ClpiExtensionData(
    uint Length,
    uint DataBlockStartAddress,
    byte NumberOfExtDataEntries,
    IReadOnlyList<ClpiExtDataEntry> ExtDataEntries,
    byte[] DataBlock)
{
    public static ClpiExtensionData Read(Stream stream)
    {
        var length = stream.ReadUInt32BigEndian();
        if (length == 0)
        {
            return new ClpiExtensionData(length, 0, 0, [], []);
        }

        using var container = MplsBoundedStream.Create(stream, length, 8, ClpiParseLimits.MaximumExtensionDataLength, "extension data");
        var dataBlockStartAddress = container.ReadUInt32BigEndian();
        container.SkipBytes(3);
        var numberOfEntries = container.ReadByteChecked();
        ClpiParseLimits.ValidateCount(numberOfEntries, 1024, "extension data entry");
        if (8L + numberOfEntries * 12L > length)
        {
            throw new InvalidDataException("CLPI extension data length cannot contain its declared entries.");
        }

        var entries = new List<ClpiExtDataEntry>(numberOfEntries);
        for (var i = 0; i < numberOfEntries; i++)
        {
            var entry = ClpiExtDataEntry.Read(container);
            if ((ulong)entry.ExtDataStartAddress + entry.ExtDataLength > length + 4UL)
            {
                throw new InvalidDataException("CLPI extension data entry exceeds the extension section.");
            }

            entries.Add(entry);
        }

        if (dataBlockStartAddress < 12L + numberOfEntries * 12L || dataBlockStartAddress > length + 4L)
        {
            throw new InvalidDataException("CLPI extension data block start address exceeds extension length.");
        }

        var dataBlockLength = length + 4L - dataBlockStartAddress;
        container.Position = dataBlockStartAddress - 4L;
        var dataBlock = container.ReadExactBytes((int)dataBlockLength);
        container.Complete("extension data");
        return new ClpiExtensionData(length, dataBlockStartAddress, numberOfEntries, entries, dataBlock);
    }
}

internal sealed record ClpiExtDataEntry(
    ushort ExtDataType,
    ushort ExtDataVersion,
    uint ExtDataStartAddress,
    uint ExtDataLength)
{
    public static ClpiExtDataEntry Read(Stream stream) =>
        new(
            stream.ReadUInt16BigEndian(),
            stream.ReadUInt16BigEndian(),
            stream.ReadUInt32BigEndian(),
            stream.ReadUInt32BigEndian());
}
