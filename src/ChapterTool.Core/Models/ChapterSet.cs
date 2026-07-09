namespace ChapterTool.Core.Models;

/// <summary>
/// Represents a chapter collection and source metadata loaded from or written to a media source.
/// </summary>
/// <param name="Title">The Title value.</param>
/// <param name="SourceName">The SourceName value.</param>
/// <param name="ImportFormat">The ImportFormat value.</param>
/// <param name="FramesPerSecond">The FramesPerSecond value.</param>
/// <param name="Duration">The Duration value.</param>
/// <param name="Chapters">The Chapters value.</param>
public sealed record ChapterSet(
    string Title,
    string? SourceName,
    ChapterImportFormat ImportFormat,
    double FramesPerSecond,
    TimeSpan Duration,
    IReadOnlyList<Chapter> Chapters);
