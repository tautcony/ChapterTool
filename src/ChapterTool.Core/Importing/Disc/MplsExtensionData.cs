namespace ChapterTool.Core.Importing.Disc;

internal sealed record MplsExtensionData(
    uint Length,
    uint DataBlockStartAddress,
    byte NumberOfExtDataEntries,
    IReadOnlyList<MplsExtDataEntry> ExtDataEntries,
    byte[] DataBlock)
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
            return new MplsExtensionData(length, 0, 0, [], []);
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
            entries.Add(MplsExtDataEntry.Read(container));
        }

        if (dataBlockStartAddress > length || dataBlockStartAddress < 8L + numberOfExtDataEntries * 12L)
        {
            throw new InvalidDataException("MPLS extension data block start address exceeds extension length.");
        }

        var dataBlockLength = length - dataBlockStartAddress;
        container.Position = dataBlockStartAddress;
        var dataBlock = container.ReadExactBytes((int)dataBlockLength);
        container.Complete("extension data");
        return new MplsExtensionData(length, dataBlockStartAddress, numberOfExtDataEntries, entries, dataBlock);
    }
}
