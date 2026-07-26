namespace ChapterTool.Core.Importing.Disc;

internal sealed record MplsPlayListMark(
    uint Length,
    ushort NumberOfPlayListMarks,
    IReadOnlyList<MplsMark> Marks)
{
    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsPlayListMark Read(Stream stream)
    {
        var length = stream.ReadUInt32BigEndian();
        using var container = stream.CreateMplsContainer(length, 2, MplsParseLimits.MaximumMarkTableLength, "playlist mark table");
        var numberOfPlayListMarks = container.ReadUInt16BigEndian();
        MplsParseLimits.ValidateCount(numberOfPlayListMarks, MplsParseLimits.MaximumPlayListMarks, "playlist mark");
        if (2L + numberOfPlayListMarks * 14L > length)
        {
            throw new InvalidDataException("MPLS playlist mark table length cannot contain its declared marks.");
        }
        MplsParseLimits.ValidateCountByBudget(numberOfPlayListMarks, 14, container.Remaining, "playlist mark");
        var marks = new List<MplsMark>(numberOfPlayListMarks);
        for (var i = 0; i < numberOfPlayListMarks; i++)
        {
            marks.Add(MplsMark.Read(container));
        }

        container.Complete("playlist mark table");
        return new MplsPlayListMark(length, numberOfPlayListMarks, marks);
    }
}
