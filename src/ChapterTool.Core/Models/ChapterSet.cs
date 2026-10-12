namespace ChapterTool.Core.Models;

/// <summary>Represents chapters and source metadata with one canonical current frame rate.</summary>
public sealed record ChapterSet(
    string Title,
    string? SourceName,
    ChapterImportFormat ImportFormat,
    FrameRateValue FrameRate,
    TimeSpan Duration,
    IReadOnlyList<Chapter> Chapters);
