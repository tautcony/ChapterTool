namespace ChapterTool.Core.Models;

/// <summary>
/// Represents a chapter collection and source metadata loaded from or written to a media source.
/// </summary>
/// <param name="Title">The display title for the chapter collection.</param>
/// <param name="SourceName">The source file, playlist, or stream name associated with the chapters.</param>
/// <param name="ImportFormat">The format that produced or should represent the chapter data.</param>
/// <param name="FramesPerSecond">The frame rate associated with frame-based chapter calculations.</param>
/// <param name="Duration">The total duration covered by the chapter collection.</param>
/// <param name="Chapters">The ordered chapter entries in the collection.</param>
public sealed record ChapterSet(
    string Title,
    string? SourceName,
    ChapterImportFormat ImportFormat,
    double FramesPerSecond,
    TimeSpan Duration,
    IReadOnlyList<Chapter> Chapters);
