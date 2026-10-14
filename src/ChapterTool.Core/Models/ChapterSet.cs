namespace ChapterTool.Core.Models;

/// <summary>Represents chapters and source metadata with one canonical current frame rate.</summary>
/// <param name="Title">The document or source title.</param>
/// <param name="SourceName">The source file name, or <see langword="null"/> when absent.</param>
/// <param name="ImportFormat">The import format that produced this set.</param>
/// <param name="FrameRate">The canonical frame rate for this set.</param>
/// <param name="Duration">The known total duration. Read <see cref="DurationIsKnown"/> first.</param>
/// <param name="Chapters">The chapter list in document order.</param>
public sealed record ChapterSet(
    string Title,
    string? SourceName,
    ChapterImportFormat ImportFormat,
    FrameRateValue FrameRate,
    TimeSpan Duration,
    IReadOnlyList<Chapter> Chapters)
{
    /// <summary>
    /// Gets whether <see cref="Duration"/> is authoritative. A portable payload can report an
    /// unknown duration. An unknown duration is not a zero duration, so callers must not use it
    /// as a segment offset. Legacy zero durations default to unknown. Set this property to
    /// true for an authoritative zero duration.
    /// </summary>
    public bool DurationIsKnown { get; init; } = Duration > TimeSpan.Zero;
}
