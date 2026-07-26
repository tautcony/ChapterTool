namespace ChapterTool.Core.Importing.Disc;

internal sealed record MplsSubPlayItem(
    ushort Length,
    MplsClipName ClipName,
    byte FlagField,
    byte RefToSTCID,
    uint INTime,
    uint OUTTime,
    ushort SyncPlayItemID,
    uint SyncStartPTS,
    byte NumberOfMultiClipEntries,
    IReadOnlyList<MplsClipNameWithRef> MultiClipEntries)
{
    /// <summary>
    /// Gets the ConnectionCondition value.
    /// </summary>
    public byte ConnectionCondition => (byte)((FlagField >> 1) & 0x0f);

    /// <summary>
    /// Gets the IsMultiClipEntries value.
    /// </summary>
    public bool IsMultiClipEntries => (FlagField & 1) == 1;

    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsSubPlayItem Read(Stream stream)
    {
        var length = stream.ReadUInt16BigEndian();
        using var container = stream.CreateMplsContainer(length, 28, MplsParseLimits.MaximumSubPlayItemLength, "subplay item");
        var clipName = MplsClipName.Read(container);
        container.SkipBytes(3);
        var flagField = container.ReadByteChecked();
        var refToSTCID = container.ReadByteChecked();
        var inTime = container.ReadUInt32BigEndian();
        var outTime = container.ReadUInt32BigEndian();
        var syncPlayItemId = container.ReadUInt16BigEndian();
        var syncStartPts = container.ReadUInt32BigEndian();
        var numberOfMultiClipEntries = (byte)0;
        var multiClipEntries = new List<MplsClipNameWithRef>();
        if ((flagField & 1) == 1)
        {
            numberOfMultiClipEntries = container.ReadByteChecked();
            MplsParseLimits.ValidateCount(numberOfMultiClipEntries, MplsParseLimits.MaximumMultiClipEntries, "multi-clip entry");
            MplsParseLimits.ValidateCountByBudget(numberOfMultiClipEntries, 10, container.Remaining - 1, "multi-clip entry");
            container.SkipBytes(1);
            for (var i = 0; i < numberOfMultiClipEntries; i++)
            {
                multiClipEntries.Add(MplsClipNameWithRef.Read(container));
            }
        }

        container.Complete("subplay item");
        return new MplsSubPlayItem(
            length,
            clipName,
            flagField,
            refToSTCID,
            inTime,
            outTime,
            syncPlayItemId,
            syncStartPts,
            numberOfMultiClipEntries,
            multiClipEntries);
    }
}
