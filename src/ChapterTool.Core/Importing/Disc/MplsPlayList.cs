namespace ChapterTool.Core.Importing.Disc;

internal sealed record MplsPlayList(
    uint Length,
    ushort NumberOfPlayItems,
    ushort NumberOfSubPaths,
    IReadOnlyList<MplsPlayItem> PlayItems,
    IReadOnlyList<MplsSubPath> SubPaths)
{
    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsPlayList Read(Stream stream)
    {
        var length = stream.ReadUInt32BigEndian();
        using var container = stream.CreateMplsContainer(length, 6, MplsParseLimits.MaximumPlaylistLength, "playlist");
        container.SkipBytes(2);
        var numberOfPlayItems = container.ReadUInt16BigEndian();
        var numberOfSubPaths = container.ReadUInt16BigEndian();
        MplsParseLimits.ValidateCount(numberOfPlayItems, MplsParseLimits.MaximumPlayItems, "play item");
        MplsParseLimits.ValidateCount(numberOfSubPaths, MplsParseLimits.MaximumSubPaths, "subpath");
        MplsParseLimits.ValidateCountByBudget(numberOfPlayItems, 2, container.Remaining, "play item");
        MplsParseLimits.ValidateCountByBudget(numberOfSubPaths, 4, container.Remaining, "subpath");
        var playItems = new List<MplsPlayItem>(numberOfPlayItems);
        for (var i = 0; i < numberOfPlayItems; i++)
        {
            playItems.Add(MplsPlayItem.Read(container));
        }

        var subPaths = new List<MplsSubPath>(numberOfSubPaths);
        for (var i = 0; i < numberOfSubPaths; i++)
        {
            subPaths.Add(MplsSubPath.Read(container));
        }

        container.Complete("playlist");
        return new MplsPlayList(length, numberOfPlayItems, numberOfSubPaths, playItems, subPaths);
    }
}
