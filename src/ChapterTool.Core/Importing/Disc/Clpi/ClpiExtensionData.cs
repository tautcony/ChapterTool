namespace ChapterTool.Core.Importing.Disc.Clpi;

#pragma warning disable SA1503

internal sealed record ClpiExtensionData(
    uint Length,
    uint DataBlockStartAddress,
    byte NumberOfExtDataEntries,
    IReadOnlyList<ClpiExtDataEntry> ExtDataEntries,
    byte[] DataBlock,
    IReadOnlyDictionary<string, byte[]> RawEntries,
    ClpiExtentStartPoints? ExtentStartPoints,
    ClpiProgramInfo? ProgramInfoSS,
    ClpiCPI? CPISS)
{
    public static ClpiExtensionData Read(Stream stream)
    {
        var length = stream.ReadUInt32BigEndian();
        if (length == 0)
        {
            return new ClpiExtensionData(length, 0, 0, [], [], new Dictionary<string, byte[]>(), null, null, null);
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
        var rawEntries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        ClpiExtentStartPoints? extentStartPoints = null;
        ClpiProgramInfo? programInfoSS = null;
        ClpiCPI? cpiSS = null;
        foreach (var entry in entries)
        {
            container.Position = entry.ExtDataStartAddress - 4L;
            var payload = container.ReadExactBytes(checked((int)entry.ExtDataLength));
            rawEntries[$"{entry.ExtDataType}.{entry.ExtDataVersion}"] = payload;
            using var payloadStream = new MemoryStream(payload, writable: false);
            switch (entry)
            {
                case { ExtDataType: 2, ExtDataVersion: 4 }:
                    extentStartPoints = ClpiExtentStartPoints.Read(payloadStream);
                    break;
                case { ExtDataType: 2, ExtDataVersion: 5 }:
                    programInfoSS = ClpiProgramInfo.Read(payloadStream);
                    break;
                case { ExtDataType: 2, ExtDataVersion: 6 }:
                    cpiSS = ClpiCPI.Read(payloadStream);
                    break;
            }
        }

        container.Complete("extension data");
        return new ClpiExtensionData(
            length,
            dataBlockStartAddress,
            numberOfEntries,
            entries,
            dataBlock,
            rawEntries,
            extentStartPoints,
            programInfoSS,
            cpiSS);
    }
}

internal sealed record ClpiExtentStartPoints(IReadOnlyList<uint> Points)
{
    internal static ClpiExtentStartPoints Read(Stream stream)
    {
        var length = stream.ReadUInt32BigEndian();
        if (length > ClpiParseLimits.MaximumExtensionDataLength || length > stream.Length - stream.Position)
            throw new InvalidDataException("CLPI extent start points exceed the extension entry.");
        var count = stream.ReadUInt32BigEndian();
        if (count > ClpiParseLimits.MaximumExtentStartPoints)
            throw new InvalidDataException("CLPI extent start point count exceeds the supported bounds.");
        ClpiParseLimits.ValidateCountByBudget(count, sizeof(uint), stream.Length - stream.Position, "extent start point");
        var points = new List<uint>(checked((int)count));
        for (var i = 0U; i < count; i++) points.Add(stream.ReadUInt32BigEndian());
        return new ClpiExtentStartPoints(points);
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
