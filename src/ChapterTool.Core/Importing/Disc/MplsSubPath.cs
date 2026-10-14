namespace ChapterTool.Core.Importing.Disc;

internal sealed record MplsSubPath(
    uint Length,
    byte SubPathType,
    ushort FlagField,
    byte NumberOfSubPlayItems,
    IReadOnlyList<MplsSubPlayItem> SubPlayItems)
{
    /// <summary>
    /// Gets a value indicating whether gets the IsRepeatSubPath value.
    /// </summary>
    public bool IsRepeatSubPath => (FlagField & 1) == 1;

    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsSubPath Read(Stream stream)
    {
        var length = stream.ReadUInt32BigEndian();
        using var container = stream.CreateMplsContainer(length, 6, MplsParseLimits.MaximumSubPathLength, "subpath");
        container.SkipBytes(1);
        var subPathType = container.ReadByteChecked();
        var flagField = container.ReadUInt16BigEndian();
        container.SkipBytes(1);
        var numberOfSubPlayItems = container.ReadByteChecked();
        MplsParseLimits.ValidateCount(numberOfSubPlayItems, MplsParseLimits.MaximumSubPlayItems, "subplay item");
        MplsParseLimits.ValidateCountByBudget(numberOfSubPlayItems, 2, container.Remaining, "subplay item");
        var subPlayItems = new List<MplsSubPlayItem>(numberOfSubPlayItems);
        for (var i = 0; i < numberOfSubPlayItems; i++)
        {
            subPlayItems.Add(MplsSubPlayItem.Read(container));
        }

        container.Complete("subpath");
        return new MplsSubPath(length, subPathType, flagField, numberOfSubPlayItems, subPlayItems);
    }
}
