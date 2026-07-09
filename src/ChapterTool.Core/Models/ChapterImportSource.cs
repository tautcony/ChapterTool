namespace ChapterTool.Core.Models;

/// <summary>
/// Represents one source path and the chapter entries discovered for that source.
/// </summary>
/// <param name="SourcePath">The path of the imported source file.</param>
/// <param name="Entries">The chapter entries discovered for the source.</param>
/// <param name="DefaultEntryIndex">The zero-based entry index selected by default.</param>
public sealed record ChapterImportSource(
    string SourcePath,
    IReadOnlyList<ChapterImportEntry> Entries,
    int DefaultEntryIndex = 0);
