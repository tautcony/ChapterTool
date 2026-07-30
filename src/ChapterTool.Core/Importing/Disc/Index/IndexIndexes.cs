namespace ChapterTool.Core.Importing.Disc.Index;

internal sealed record IndexIndexes(
    uint Length,
    IndexTitleEntry FirstPlaybackTitle,
    IndexTitleEntry TopMenuTitle,
    IReadOnlyList<IndexTitleEntry> Titles)
{
    public static IndexIndexes Read(Stream stream)
    {
        var length = stream.ReadUInt32BigEndian();
        using var container = stream.CreateMplsContainer(
            length,
            IndexParseLimits.MinimumIndexesLength,
            IndexParseLimits.MaximumIndexesLength,
            "indexes");
        var firstPlaybackTitle = IndexTitleEntry.Read(container);
        var topMenuTitle = IndexTitleEntry.Read(container);
        var numberOfTitles = container.ReadUInt16BigEndian();
        MplsParseLimits.ValidateCount(numberOfTitles, IndexParseLimits.MaximumTitles, "index titles");
        MplsParseLimits.ValidateCountByBudget(
            numberOfTitles,
            IndexTitleEntry.SerializedLength,
            container.Remaining,
            "index titles");

        var titles = new List<IndexTitleEntry>(numberOfTitles);
        for (var i = 0; i < numberOfTitles; i++)
        {
            titles.Add(IndexTitleEntry.Read(container));
        }

        container.Complete("indexes");
        return new IndexIndexes(length, firstPlaybackTitle, topMenuTitle, titles);
    }

    public IEnumerable<IndexTitleEntry> MovieTitles =>
        Titles.Where(static title => title.IsMovieObject && title.IsMoviePlayback);
}
