namespace ChapterTool.Core.Importing.Disc;

internal sealed record MplsExtensionData(
    uint Length,
    uint DataBlockStartAddress,
    byte NumberOfExtDataEntries,
    IReadOnlyList<MplsExtDataEntry> ExtDataEntries,
    byte[] DataBlock,
    IReadOnlyList<MplsPipMetadata> PipMetadata,
    IReadOnlyList<MplsSubPath> ExtensionSubPaths,
    IReadOnlyList<MplsStaticMetadata> StaticMetadata)
{
    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsExtensionData Read(Stream stream)
    {
        var length = stream.ReadUInt32BigEndian();
        if (length == 0)
        {
            return new MplsExtensionData(length, 0, 0, [], [], [], [], []);
        }

        using var container = stream.CreateMplsContainer(length, 8, MplsParseLimits.MaximumExtensionDataLength, "extension data");
        var dataBlockStartAddress = container.ReadUInt32BigEndian();
        container.SkipBytes(3);
        var numberOfExtDataEntries = container.ReadByteChecked();
        MplsParseLimits.ValidateCount(numberOfExtDataEntries, MplsParseLimits.MaximumExtensionEntries, "extension data entry");
        if (8L + numberOfExtDataEntries * 12L > length)
        {
            throw new InvalidDataException("MPLS extension data length cannot contain its declared entries.");
        }
        MplsParseLimits.ValidateCountByBudget(numberOfExtDataEntries, 12, container.Remaining, "extension data entry");
        var entries = new List<MplsExtDataEntry>(numberOfExtDataEntries);
        for (var i = 0; i < numberOfExtDataEntries; i++)
        {
            var entry = MplsExtDataEntry.Read(container);
            if ((ulong)entry.ExtDataStartAddress + entry.ExtDataLength > length + 4UL)
            {
                throw new InvalidDataException("MPLS extension data entry exceeds the extension section.");
            }

            entries.Add(entry);
        }

        if (dataBlockStartAddress > length + 4L || dataBlockStartAddress < 12L + numberOfExtDataEntries * 12L)
        {
            throw new InvalidDataException("MPLS extension data block start address exceeds extension length.");
        }

        var dataBlockLength = length + 4L - dataBlockStartAddress;
        container.Position = dataBlockStartAddress - 4L;
        var dataBlock = container.ReadExactBytes((int)dataBlockLength);
        container.Complete("extension data");

        var pipMetadata = new List<MplsPipMetadata>();
        var extensionSubPaths = new List<MplsSubPath>();
        var staticMetadata = new List<MplsStaticMetadata>();
        foreach (var entry in entries)
        {
            if (entry.ExtDataStartAddress < dataBlockStartAddress || entry.ExtDataLength == 0)
            {
                continue;
            }

            var offset = checked((int)(entry.ExtDataStartAddress - dataBlockStartAddress));
            using var entryStream = new MemoryStream(dataBlock, writable: false);
            entryStream.Position = offset;
            using var entryContainer = MplsBoundedStream.Create(
                entryStream,
                entry.ExtDataLength,
                1,
                MplsParseLimits.MaximumExtensionDataLength,
                "extension entry");

            switch ((entry.ExtDataType, entry.ExtDataVersion))
            {
                case (1, 1):
                    pipMetadata.AddRange(ParsePipMetadata(entryContainer));
                    break;
                case (2, 2):
                    ParseSubPathMetadata(entryContainer, extensionSubPaths);
                    break;
                case (3, 5):
                    staticMetadata.AddRange(ParseStaticMetadata(entryContainer));
                    break;
            }
        }

        return new MplsExtensionData(
            length,
            dataBlockStartAddress,
            numberOfExtDataEntries,
            entries,
            dataBlock,
            pipMetadata,
            extensionSubPaths,
            staticMetadata);
    }

    private static IReadOnlyList<MplsPipMetadata> ParsePipMetadata(Stream stream)
    {
        _ = stream.ReadUInt32BigEndian();
        var entryCount = stream.ReadUInt16BigEndian();
        MplsParseLimits.ValidateCountByBudget(entryCount, 14, stream is MplsBoundedStream bounded ? bounded.Remaining : stream.Length - stream.Position, "PiP metadata entry");
        var entries = new List<MplsPipMetadata>(entryCount);
        for (var i = 0; i < entryCount; i++)
        {
            var clipReference = stream.ReadUInt16BigEndian();
            var secondaryVideoReference = stream.ReadByteChecked();
            stream.SkipBytes(1);
            var flags = stream.ReadUInt16BigEndian();
            var timelineType = (byte)(flags >> 12);
            var lumaKeyFlag = (flags & 0x0800) != 0;
            var trickPlayFlag = (flags & 0x0400) != 0;
            var upperLimitLumaKey = (byte)0;
            if (lumaKeyFlag)
            {
                stream.SkipBytes(1);
                upperLimitLumaKey = stream.ReadByteChecked();
            }
            else
            {
                stream.SkipBytes(2);
            }

            stream.SkipBytes(2);
            var dataAddress = stream.ReadUInt32BigEndian();
            var savedPosition = stream.Position;
            stream.Position = dataAddress;
            var dataCount = stream.ReadUInt16BigEndian();
            MplsParseLimits.ValidateCountByBudget(dataCount, 8, stream is MplsBoundedStream dataBounded ? dataBounded.Remaining : stream.Length - stream.Position, "PiP data");
            var data = new List<MplsPipData>(dataCount);
            for (var dataIndex = 0; dataIndex < dataCount; dataIndex++)
            {
                var time = stream.ReadUInt32BigEndian();
                var packed = stream.ReadUInt32BigEndian();
                data.Add(new MplsPipData(
                    time,
                    (ushort)(packed >> 20),
                    (ushort)((packed >> 8) & 0x0fff),
                    (byte)((packed >> 4) & 0x0f)));
            }

            stream.Position = savedPosition;
            entries.Add(new MplsPipMetadata(
                clipReference,
                secondaryVideoReference,
                timelineType,
                lumaKeyFlag,
                upperLimitLumaKey,
                trickPlayFlag,
                data));
        }

        return entries;
    }

    private static void ParseSubPathMetadata(Stream stream, List<MplsSubPath> destination)
    {
        _ = stream.ReadUInt32BigEndian();
        var count = stream.ReadUInt16BigEndian();
        MplsParseLimits.ValidateCountByBudget(count, 4, stream is MplsBoundedStream bounded ? bounded.Remaining : stream.Length - stream.Position, "extension subpath");
        for (var i = 0; i < count; i++)
        {
            destination.Add(MplsSubPath.Read(stream));
        }
    }

    private static IReadOnlyList<MplsStaticMetadata> ParseStaticMetadata(Stream stream)
    {
        _ = stream.ReadUInt32BigEndian();
        var count = stream.ReadByteChecked();
        stream.SkipBytes(3);
        MplsParseLimits.ValidateCountByBudget(count, 28, stream is MplsBoundedStream bounded ? bounded.Remaining : stream.Length - stream.Position, "static metadata");
        var result = new List<MplsStaticMetadata>(count);
        for (var i = 0; i < count; i++)
        {
            var dynamicRangeType = (byte)(stream.ReadByteChecked() >> 4);
            stream.SkipBytes(3);
            var primaryX = new ushort[3];
            var primaryY = new ushort[3];
            for (var primary = 0; primary < 3; primary++)
            {
                primaryX[primary] = stream.ReadUInt16BigEndian();
                primaryY[primary] = stream.ReadUInt16BigEndian();
            }

            result.Add(new MplsStaticMetadata(
                dynamicRangeType,
                primaryX,
                primaryY,
                stream.ReadUInt16BigEndian(),
                stream.ReadUInt16BigEndian(),
                stream.ReadUInt16BigEndian(),
                stream.ReadUInt16BigEndian(),
                stream.ReadUInt16BigEndian(),
                stream.ReadUInt16BigEndian()));
        }

        return result;
    }
}
