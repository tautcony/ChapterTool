namespace ChapterTool.Core.Models;

/// <summary>
/// Represents one source path and the chapter entries discovered for that source.
/// </summary>
/// <param name="SourcePath">The SourcePath value.</param>
/// <param name="Entries">The Entries value.</param>
/// <param name="DefaultEntryIndex">The DefaultEntryIndex value.</param>
public sealed record ChapterImportSource(
    string SourcePath,
    IReadOnlyList<ChapterImportEntry> Entries,
    int DefaultEntryIndex = 0);
