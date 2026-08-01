namespace ChapterTool.Core.Models;

/// <summary>
/// Contains the semantic values that hosts use to render one import option.
/// </summary>
/// <param name="MainText">The importer's display name.</param>
/// <param name="ChapterCount">The number of chapters in the option.</param>
public sealed record ChapterImportDisplay(string MainText, int ChapterCount)
{
    /// <summary>Creates display values from one imported entry.</summary>
    /// <param name="entry">The imported entry.</param>
    /// <returns>The display values for the entry.</returns>
    public static ChapterImportDisplay From(ChapterImportEntry entry) =>
        new(entry.DisplayName, entry.ChapterSet.Chapters.Count);
}
